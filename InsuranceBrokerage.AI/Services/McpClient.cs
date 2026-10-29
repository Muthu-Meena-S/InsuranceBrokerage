using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InsuranceBrokerage.AI.Models;

namespace InsuranceBrokerage.AI.Services;

public class McpClient : IMcpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly ILogger<McpClient> _logger;
    private int _requestId = 1;
    private List<string>? _cachedToolNames;

    public McpClient(HttpClient httpClient, IConfiguration configuration, ILogger<McpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _endpoint = configuration["McpServer:BaseUrl"] ?? "http://localhost:3001/mcp";
    }

    public async Task<List<McpToolDefinition>> GetToolsAsync()
    {
        try
        {
            var request = new JsonRpcRequest
            {
                Id = Interlocked.Increment(ref _requestId),
                Method = "tools/list",
                Params = new { }
            };

            var responseJson = await SendJsonRpcAsync(request);
            if (string.IsNullOrWhiteSpace(responseJson))
                return new List<McpToolDefinition>();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("result", out var resultElement) &&
                resultElement.TryGetProperty("tools", out var toolsElement))
            {
                var tools = JsonSerializer.Deserialize<List<McpToolDefinition>>(toolsElement.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tools != null)
                {
                    _cachedToolNames = tools.Select(t => t.Name).ToList();
                }

                return tools ?? new List<McpToolDefinition>();
            }

            return new List<McpToolDefinition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch tools from MCP server at {Endpoint}", _endpoint);
            return new List<McpToolDefinition>();
        }
    }

    public async Task<(bool Success, string Content)> CallToolAsync(string toolName, Dictionary<string, object?> arguments)
    {
        try
        {
            // Resolve tool name to match MCP server registered name (handles GetLeads vs get_leads)
            var resolvedName = ResolveToolName(toolName);

            var request = new JsonRpcRequest
            {
                Id = Interlocked.Increment(ref _requestId),
                Method = "tools/call",
                Params = new
                {
                    name = resolvedName,
                    arguments = arguments
                }
            };

            var responseJson = await SendJsonRpcAsync(request);
            if (string.IsNullOrWhiteSpace(responseJson))
                return (false, "Empty response from MCP server.");

            using var doc = JsonDocument.Parse(responseJson);

            // Check for JSON-RPC error
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                var errorMsg = errorElement.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                return (false, $"MCP Error: {errorMsg}");
            }

            // Parse result
            if (doc.RootElement.TryGetProperty("result", out var resultElement))
            {
                // In MCP, result has content array: [{ type: "text", text: "..." }]
                if (resultElement.TryGetProperty("content", out var contentArray) &&
                    contentArray.ValueKind == JsonValueKind.Array &&
                    contentArray.GetArrayLength() > 0)
                {
                    var firstItem = contentArray[0];
                    if (firstItem.TryGetProperty("text", out var textVal))
                    {
                        return (true, textVal.GetString() ?? string.Empty);
                    }
                }

                return (true, resultElement.GetRawText());
            }

            return (true, responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool {ToolName}", toolName);
            return (false, $"Failed to execute tool {toolName}: {ex.Message}");
        }
    }

    private string ResolveToolName(string requestedName)
    {
        if (_cachedToolNames == null || _cachedToolNames.Count == 0)
            return requestedName;

        // Exact match
        var exact = _cachedToolNames.FirstOrDefault(n => n.Equals(requestedName, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Match ignoring underscores and hyphens (e.g. GetLeads vs get_leads)
        var cleanRequested = requestedName.Replace("_", "").Replace("-", "");
        var fuzzy = _cachedToolNames.FirstOrDefault(n => n.Replace("_", "").Replace("-", "").Equals(cleanRequested, StringComparison.OrdinalIgnoreCase));
        if (fuzzy != null) return fuzzy;

        return requestedName;
    }

    private async Task<string> SendJsonRpcAsync(JsonRpcRequest rpcRequest)
    {
        var json = JsonSerializer.Serialize(rpcRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        // MCP Streamable HTTP transport requires both JSON and SSE in the Accept header
        httpRequest.Headers.Accept.ParseAdd("application/json");
        httpRequest.Headers.Accept.ParseAdd("text/event-stream");
        httpRequest.Content = content;

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();

        // If the MCP server responds with SSE format (text/event-stream),
        // extract the JSON payload from the "data:" lines.
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (contentType.Contains("text/event-stream") || responseBody.TrimStart().StartsWith("event:") || responseBody.TrimStart().StartsWith("data:"))
        {
            return ExtractJsonFromSse(responseBody);
        }

        return responseBody;
    }

    /// <summary>
    /// Parses an SSE payload and returns the JSON from the last "data:" line
    /// that contains a valid JSON object (skips "data: [DONE]" etc.).
    /// </summary>
    private static string ExtractJsonFromSse(string sseBody)
    {
        string? lastDataJson = null;

        foreach (var line in sseBody.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var data = trimmed.Substring(5).Trim();
                if (!string.IsNullOrWhiteSpace(data) && data != "[DONE]" && data.StartsWith("{"))
                {
                    lastDataJson = data;
                }
            }
        }

        return lastDataJson
            ?? throw new InvalidOperationException("No valid JSON data line found in SSE response.");
    }
}

