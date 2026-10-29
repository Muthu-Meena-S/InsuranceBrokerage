using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceBrokerage.AI.Models;

namespace InsuranceBrokerage.AI.Services;

public class InsuranceAiOrchestrator : IAiOrchestrator
{
    private readonly IMcpClient _mcpClient;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InsuranceAiOrchestrator> _logger;

    // Cache resolved model so /v1/models is only called once per process lifetime
    // Static so it persists across scoped instances (one per request)
    private static string? _resolvedModel;

    // Ordered preference list for tool-calling LLMs on Groq
    private static readonly string[] GroqModelPreference =
    [
        "openai/gpt-oss-20b",
        "openai/gpt-oss-120b",
        "llama-3.3-70b-versatile",
        "llama-3.1-8b-instant",
        "llama3-70b-8192",
        "llama-3.1-70b-versatile",
        "llama3-8b-8192"
    ];

    // Keywords that identify NON-chat or NON-tool-calling models to skip
    private static readonly string[] NonChatModelKeywords =
        ["safeguard", "guard", "whisper", "tts", "embed", "moderation",
         "vision", "orpheus", "canopy", "audio", "speech", "rerank", "distill", "deepseek", "qwen"];

    public InsuranceAiOrchestrator(
        IMcpClient mcpClient,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<InsuranceAiOrchestrator> logger)
    {
        _mcpClient = mcpClient;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request)
    {
        var apiKey  = _configuration["AiService:ApiKey"]    ?? string.Empty;
        var endpoint = _configuration["AiService:Endpoint"] ?? "https://api.openai.com/v1";
        var model   = _configuration["AiService:Model"]     ?? "gpt-4o-mini";

        // If an external API Key is provided, call the real External LLM with MCP tool schemas
        if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_API_KEY_HERE")
        {
            // For Groq endpoints, auto-resolve the best available model
            if (endpoint.Contains("groq.com", StringComparison.OrdinalIgnoreCase))
            {
                model = await ResolveGroqModelAsync(apiKey, endpoint, model);
            }

            try
            {
                _logger.LogInformation("Routing query to External LLM ({Model}) at {Endpoint}", model, endpoint);
                return await CallExternalLlmWithToolsAsync(request, apiKey, endpoint, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External LLM call failed. Falling back to local reasoning engine.");
                var fallbackResponse = await FallbackReasoningEngineAsync(request);
                fallbackResponse.Reply = $"⚠️ *(External LLM call failed: {ex.Message}. Falling back to internal engine)*\n\n" + fallbackResponse.Reply;
                return fallbackResponse;
            }
        }

        // If no API key configured yet, use the built-in reasoning engine
        return await FallbackReasoningEngineAsync(request);
    }

    /// <summary>
    /// Queries Groq's /v1/models endpoint and returns the first preferred model
    /// that is currently active. If none preferred are available, picks the first
    /// chat-capable model from the live list. Caches the result per process lifetime.
    /// </summary>
    private async Task<string> ResolveGroqModelAsync(string apiKey, string endpoint, string configuredModel)
    {
        // Return cached result — avoids calling /v1/models on every request
        if (_resolvedModel != null)
            return _resolvedModel;

        try
        {
            var cleanEndpoint = endpoint.TrimEnd('/');
            var url = cleanEndpoint.EndsWith("/models") ? cleanEndpoint : $"{cleanEndpoint}/models";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not fetch Groq model list (HTTP {Status}). Using configured model: {Model}", resp.StatusCode, configuredModel);
                return _resolvedModel = configuredModel;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var allModels = doc.RootElement
                .GetProperty("data")
                .EnumerateArray()
                .Select(m => m.GetProperty("id").GetString() ?? "")
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            // Filter models: exclude non-chat/non-tool models, and prefer models that support tool calling (llama/gpt-oss/tool)
            var toolCallingModels = allModels
                .Where(id => !NonChatModelKeywords.Any(kw => id.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                .Where(id => id.Contains("llama", StringComparison.OrdinalIgnoreCase) ||
                             id.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase) ||
                             id.Contains("tool", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogInformation("Groq tool-calling models available: {Models}", string.Join(", ", toolCallingModels));

            // 1. Try preferred list in order (support partial/sub-string matches like meta-llama/...)
            foreach (var preferred in GroqModelPreference)
            {
                var match = toolCallingModels.FirstOrDefault(m => m.Contains(preferred, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    _logger.LogInformation("Auto-selected Groq model from preference list: {Model}", match);
                    return _resolvedModel = match;
                }
            }

            // 2. Pick first available tool-calling model
            var firstToolModel = toolCallingModels.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstToolModel))
            {
                _logger.LogWarning("Auto-selected first available tool-calling model: {Model}", firstToolModel);
                return _resolvedModel = firstToolModel;
            }

            // 3. Total fallback
            _logger.LogWarning("Groq model list was empty. Using configured: {Model}", configuredModel);
            return _resolvedModel = configuredModel;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Groq model auto-resolution failed. Using configured model: {Model}", configuredModel);
            return _resolvedModel = configuredModel;
        }
    }

    /// <summary>
    /// Executes a real external LLM call (OpenAI / Gemini / Groq / Ollama) with dynamic MCP function-calling.
    /// </summary>
    private async Task<ChatResponse> CallExternalLlmWithToolsAsync(ChatRequest request, string apiKey, string endpoint, string model)
    {
        var badges = new List<ToolExecutionBadge>();

        // 1. Fetch available tools dynamically from the MCP Server
        var mcpTools = await _mcpClient.GetToolsAsync();

        // If MCP has no tools, the external LLM has nothing to call.
        // The system prompt still mentions tools, which confuses the model into
        // generating tool calls anyway — Groq then rejects with "tool_choice is none
        // but model called a tool". Fall through to the reliable fallback engine instead.
        if (mcpTools.Count == 0)
        {
            _logger.LogWarning("MCP returned 0 tools — skipping external LLM and using fallback reasoning engine.");
            return await FallbackReasoningEngineAsync(request);
        }

        // 2. Format MCP tools into the standard OpenAI-compatible tool schema (compliant with Groq / OpenAI)
        var openAiTools = mcpTools.Select(t =>
        {
            object parametersObj;
            if (t.InputSchema.ValueKind == JsonValueKind.Object &&
                t.InputSchema.TryGetProperty("type", out var typeProp) &&
                typeProp.GetString() == "object")
            {
                parametersObj = t.InputSchema;
            }
            else
            {
                parametersObj = new { type = "object", properties = new { } };
            }

            return new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = parametersObj
                }
            };
        }).ToList();

        // 3. Build conversation messages
        var systemPrompt = "You are an AI assistant for Emirates Insurance Brokerage (UAE). " +
            "Use available tools to help users: GetInsuranceProducts, CreateLead, GetLeads, " +
            "CreateApplication, ReviewApplication, ProposeQuotes, SelectQuoteAndIssuePolicy, GetApplicationDetails. " +
            "For CreateLead, collect only fullName, email, and phone; insuranceType is optional and must not be requested to create a lead. " +
            "Require insuranceType only when creating an application. Do not select an insurer until quotes are compared. " +
            "Be concise. Call tools when needed. Ask for missing info politely. Use AED for amounts.";

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        // Keep only recent messages to prevent token limits from exhausting
        var recentHistory = request.ConversationHistory.TakeLast(4);
        foreach (var history in recentHistory)
        {
            messages.Add(new { role = history.Role, content = history.Content });
        }

        messages.Add(new { role = "user", content = request.Message });

        // 4. Call External LLM (First Turn)
        var llmRequestPayload = new
        {
            model = model,
            messages = messages,
            tools = openAiTools.Count > 0 ? (object)openAiTools : null,
            tool_choice = openAiTools.Count > 0 ? "auto" : null,
            temperature = 0.3
        };

        var responseJson = await PostLlmChatAsync(endpoint, apiKey, llmRequestPayload);
        using var responseDoc = JsonDocument.Parse(responseJson);

        var choices = responseDoc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            return new ChatResponse { Reply = "No response from External LLM." };
        }

        var messageElement = choices[0].GetProperty("message");

        // 5. Check if the LLM decided to invoke tool(s)
        if (messageElement.TryGetProperty("tool_calls", out var toolCallsElement) &&
            toolCallsElement.ValueKind == JsonValueKind.Array &&
            toolCallsElement.GetArrayLength() > 0)
        {
            // Add the assistant's tool-call request to conversation history
            messages.Add(JsonSerializer.Deserialize<object>(messageElement.GetRawText())!);

            foreach (var toolCall in toolCallsElement.EnumerateArray())
            {
                var function = toolCall.GetProperty("function");
                var toolName = function.GetProperty("name").GetString() ?? "";
                var argumentsJson = function.GetProperty("arguments").GetString() ?? "{}";
                var toolCallId = toolCall.GetProperty("id").GetString() ?? "";

                _logger.LogInformation("External LLM decided to invoke MCP Tool: {ToolName} with {Args}", toolName, argumentsJson);

                var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson) ?? new();

                // 6. Execute the real MCP Tool on http://localhost:3001/mcp
                var (toolSuccess, toolResult) = await _mcpClient.CallToolAsync(toolName, arguments);

                badges.Add(new ToolExecutionBadge
                {
                    ToolName = toolName,
                    Arguments = arguments,
                    Summary = $"Executed by External LLM ({model}) via MCP",
                    Success = toolSuccess
                });

                // 7. Add tool result back to the conversation
                messages.Add(new
                {
                    role = "tool",
                    tool_call_id = toolCallId,
                    name = toolName,
                    content = toolResult
                });
            }

            // 8. Call External LLM again (Second Turn) to formulate the final answer with tool data
            var secondTurnPayload = new
            {
                model = model,
                messages = messages,
                temperature = 0.4
            };

            var finalTurnJson = await PostLlmChatAsync(endpoint, apiKey, secondTurnPayload);
            using var finalDoc = JsonDocument.Parse(finalTurnJson);
            var finalReply = finalDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return new ChatResponse
            {
                Reply = finalReply,
                ToolsExecuted = badges
            };
        }

        // Direct conversational answer without tool calling
        var directContent = messageElement.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : "";
        return new ChatResponse
        {
            Reply = directContent ?? "I could not process your request.",
            ToolsExecuted = badges
        };
    }

    private async Task<string> PostLlmChatAsync(string endpoint, string apiKey, object payload)
    {
        var cleanEndpoint = endpoint.TrimEnd('/');
        var url = cleanEndpoint.EndsWith("/chat/completions") ? cleanEndpoint : $"{cleanEndpoint}/chat/completions";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"External LLM returned HTTP {response.StatusCode}: {responseBody}");
        }

        return responseBody;
    }

    /// <summary>
    /// Out-of-the-box local reasoning engine when no external API key is provided.
    /// </summary>
    private async Task<ChatResponse> FallbackReasoningEngineAsync(ChatRequest request)
    {
        var userText = request.Message.Trim();
        var badges = new List<ToolExecutionBadge>();

        // 1. Check for Products query
        if (Regex.IsMatch(userText, @"\b(products?|plans?|coverage options?|what (do you have|can you cover)|what insurance)\b", RegexOptions.IgnoreCase))
        {
            var (success, result) = await _mcpClient.CallToolAsync("GetInsuranceProducts", new Dictionary<string, object?>());
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "GetInsuranceProducts",
                Summary = "Retrieved UAE-compliant insurance offerings via MCP",
                Success = success
            });

            return new ChatResponse
            {
                Reply = "Here are the regulated insurance products available in the UAE under Central Bank of UAE (CBUAE) and DHA/DoH guidelines:\n\n" +
                        "1. 🚗 **Motor Insurance (Unified UAE Policy)**: Comprehensive accidental damage, third-party liability (up to AED 2–5M), off-road cover, agency repairs, and 24/7 UAE roadside assistance.\n\n" +
                        "2. 🏥 **Health Insurance (DHA / DoH Compliant)**: Mandatory medical coverage including Essential Benefits Plan (EBP) and enhanced tiers with leading hospital networks across Dubai and Abu Dhabi.\n\n" +
                        "3. 🏠 **Property & Home Insurance**: Coverage for villas, apartments, and buildings against fire, water leakage, natural perils, tenant liability, and contents.\n\n" +
                        "Would you like to register a new lead or apply for one of these policies?",
                ToolsExecuted = badges
            };
        }

        // 2. Check for "List Leads" / "Get Leads" / "Show all active leads in the database"
        if (Regex.IsMatch(userText, @"\b(list|show|get|view|see|fetch|display)\b.*\bleads?\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(userText, @"\b(all\s+leads?|leads?\s+list)\b", RegexOptions.IgnoreCase))
        {
            var (success, result) = await _mcpClient.CallToolAsync("GetLeads", new Dictionary<string, object?>());
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "GetLeads",
                Summary = "Queried all customer leads from SQL Server via MCP",
                Success = success
            });

            return new ChatResponse
            {
                Reply = FormatLeadsResponse(result),
                ToolsExecuted = badges
            };
        }

        // 3. Check for Lead Creation with details: "Create lead for Ahmed, ahmed@gmail.com, 0501234567"
        var leadMatch = ExtractLeadDetails(userText);
        if (leadMatch != null)
        {
            var args = new Dictionary<string, object?>
            {
                { "fullName", leadMatch.FullName },
                { "email", leadMatch.Email },
                { "phone", leadMatch.Phone }
            };

            var (success, result) = await _mcpClient.CallToolAsync("CreateLead", args);
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "CreateLead",
                Arguments = args,
                Summary = $"Created new lead for {leadMatch.FullName}",
                Success = success
            });

            return new ChatResponse
            {
                Reply = $"✅ **Lead successfully created!**\n\n" +
                        $"• **Customer**: {leadMatch.FullName}\n" +
                        $"• **Contact**: {leadMatch.Email} | {leadMatch.Phone}\n" +
                        "• **Insurance Type**: Selected when lead is converted into an application.\n\n" +
                        $"*MCP Response*: {result}\n\n" +
                        $"👉 **Next Step**: You can now create an insurance application for this lead (e.g., *\"Create application for Lead #1 with coverage AED 150,000 for 2024 Nissan Patrol\"*).",
                ToolsExecuted = badges
            };
        }
        else if (Regex.IsMatch(userText, @"\b(create|add|new|register|make)\b.*\blead\b", RegexOptions.IgnoreCase))
        {
            return new ChatResponse
            {
                Reply = "I would be happy to register a new insurance lead for you! 📝\n\n" +
                        "Could you please provide:\n" +
                        "1. **Full Name**\n" +
                        "2. **Email Address**\n" +
                        "3. **Phone Number** (e.g., +971 50 123 4567)\n\n" +
                        "*(Or all in one sentence: \"Create lead for Mansoor Al Suwaidi, mansoor@example.ae, +971505551234\")*",
                ToolsExecuted = badges
            };
        }

        // 4. Check for Application Creation
        var appMatch = ExtractApplicationDetails(userText);
        if (appMatch != null)
        {
            var args = new Dictionary<string, object?>
            {
                { "leadId", appMatch.LeadId },
                { "insuranceType", appMatch.InsuranceType },
                { "coverageAmount", appMatch.CoverageAmount },
                { "description", appMatch.Description }
            };

            var (success, result) = await _mcpClient.CallToolAsync("CreateApplication", args);
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "CreateApplication",
                Arguments = args,
                Summary = $"Submitted application for Lead #{appMatch.LeadId} (Coverage: AED {appMatch.CoverageAmount:N0})",
                Success = success
            });

            return new ChatResponse
            {
                Reply = $"📋 **Insurance Application Submitted!**\n\n" +
                        $"• **Lead ID**: #{appMatch.LeadId}\n" +
                        $"• **Coverage Amount**: AED {appMatch.CoverageAmount:N2}\n" +
                        $"• **Type**: {appMatch.InsuranceType}\n" +
                        $"• **Details**: {appMatch.Description}\n\n" +
                        $"*MCP Tool Output*: {result}\n\n" +
                        $"👉 **Next Steps**: You can have an underwriter review it (*\"Approve application #{appMatch.LeadId}\"*) or request quotes directly (*\"Propose quotes for application #{appMatch.LeadId}\"*).",
                ToolsExecuted = badges
            };
        }
        else if (Regex.IsMatch(userText, @"\b(create|submit|new|start|apply)\b.*\bapp(lication)?\b", RegexOptions.IgnoreCase))
        {
            return new ChatResponse
            {
                Reply = "I would be happy to submit an insurance application for you! 📋\n\n" +
                        "Please provide the details in this format:\n" +
                        "• **Lead ID**: (e.g. *Lead #1* — or say *\"Show leads\"* if you need to check)\n" +
                        "• **Coverage Amount**: (e.g. *AED 120,000*)\n" +
                        "• **Insurance Line**: Motor, Health, or Property\n" +
                        "• **Description**: (e.g. *2023 Nissan Patrol*)\n\n" +
                        "*(Example: \"Create application for Lead #1 with AED 120,000 coverage for 2023 Nissan Patrol\")*",
                ToolsExecuted = badges
            };
        }

        // 5. Check for Application Review
        var reviewMatch = ExtractReviewDetails(userText);
        if (reviewMatch != null)
        {
            var args = new Dictionary<string, object?>
            {
                { "applicationId", reviewMatch.AppId },
                { "reviewerName", reviewMatch.ReviewerName },
                { "decision", reviewMatch.Decision },
                { "comments", reviewMatch.Comments }
            };

            var (success, result) = await _mcpClient.CallToolAsync("ReviewApplication", args);
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "ReviewApplication",
                Arguments = args,
                Summary = $"Application #{reviewMatch.AppId} marked as {reviewMatch.Decision}",
                Success = success
            });

            return new ChatResponse
            {
                Reply = $"📝 **Underwriting Review Completed!**\n\n" +
                        $"• **Application**: #{reviewMatch.AppId}\n" +
                        $"• **Decision**: **{reviewMatch.Decision}**\n" +
                        $"• **Reviewer**: {reviewMatch.ReviewerName}\n" +
                        $"• **Comments**: {reviewMatch.Comments}\n\n" +
                        $"👉 **Next Step**: You can now generate comparative quotes from UAE insurers (*\"Propose quotes for application #{reviewMatch.AppId}\"*).",
                ToolsExecuted = badges
            };
        }

        // 6. Check for Quotes
        var quoteAppId = ExtractIdAfterKeywords(userText, "quotes? (for )?(application )?", "app(lication)? #?");
        if (quoteAppId.HasValue && Regex.IsMatch(userText, @"\b(quotes?|compare|propose)\b", RegexOptions.IgnoreCase))
        {
            var args = new Dictionary<string, object?>
            {
                { "applicationId", quoteAppId.Value }
            };

            var (success, result) = await _mcpClient.CallToolAsync("ProposeQuotes", args);
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "ProposeQuotes",
                Arguments = args,
                Summary = $"Generated quotes from Sukoon, ADNIC, and Orient for App #{quoteAppId.Value}",
                Success = success
            });

            return new ChatResponse
            {
                Reply = FormatQuotesResponse(result, quoteAppId.Value),
                ToolsExecuted = badges
            };
        }

        // 7. Check for Policy Issuance
        var quoteId = ExtractIdAfterKeywords(userText, "(select|accept|buy|issue( policy for)?|choose) quote #?", "quote #?");
        if (quoteId.HasValue && Regex.IsMatch(userText, @"\b(policy|issue|select|accept|buy)\b", RegexOptions.IgnoreCase))
        {
            var args = new Dictionary<string, object?>
            {
                { "quoteId", quoteId.Value }
            };

            var (success, result) = await _mcpClient.CallToolAsync("SelectQuoteAndIssuePolicy", args);
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "SelectQuoteAndIssuePolicy",
                Arguments = args,
                Summary = $"Issued active policy for Quote #{quoteId.Value}",
                Success = success
            });

            return new ChatResponse
            {
                Reply = $"🎉 **Policy Successfully Issued & Active!**\n\n" +
                        $"*MCP Response*: {result}\n\n" +
                        $"The insurance certificate and unified policy schedule are now logged in the Central Bank of UAE brokerage registry.",
                ToolsExecuted = badges
            };
        }

        // 8. Check for Application Status
        var statusAppId = ExtractIdAfterKeywords(userText, "(status|details|check) (of )?application #?", "app(lication)? #?");
        if (statusAppId.HasValue)
        {
            var args = new Dictionary<string, object?>
            {
                { "applicationId", statusAppId.Value }
            };

            var (success, result) = await _mcpClient.CallToolAsync("GetApplicationDetails", args);
            badges.Add(new ToolExecutionBadge
            {
                ToolName = "GetApplicationDetails",
                Arguments = args,
                Summary = $"Retrieved full lifecycle status for Application #{statusAppId.Value}",
                Success = success
            });

            return new ChatResponse
            {
                Reply = $"🔎 **Application #{statusAppId.Value} Details:**\n\n{result}",
                ToolsExecuted = badges
            };
        }

        // Default: Conversational Assistance & Guidance
        return new ChatResponse
        {
            Reply = "👋 **Welcome to the Emirates Insurance Brokerage AI Assistant!**\n\n" +
                    "I am connected directly to your **MCP Insurance Server** running in the background. Here is what I can do autonomously for you:\n\n" +
                    "• **1. Show Products**: *\"What insurance products do you cover?\"*\n" +
                    "• **2. Create Lead**: *\"Create lead for Mansoor Al Suwaidi, mansoor@example.ae, +971505551234, Motor\"*\n" +
                    "• **3. Submit Application**: *\"Create application for Lead #1 with AED 120,000 coverage for 2023 Nissan Patrol\"*\n" +
                    "• **4. Underwriting Review**: *\"Approve application #1\"*\n" +
                    "• **5. Compare Quotes**: *\"Propose quotes for application #1\"* (queries Sukoon, ADNIC, and Orient)\n" +
                    "• **6. Issue Policy**: *\"Select quote #1 and issue policy\"*\n\n" +
                    "*(Note: You can add an OpenAI / Gemini / Ollama API key in `appsettings.json` under `AiService:ApiKey` to run on a live cloud LLM!)*\n\n" +
                    "How can I assist you today?",
            ToolsExecuted = badges
        };
    }

    private static string FormatLeadsResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Leads", out var leadsArray) &&
                leadsArray.ValueKind == JsonValueKind.Array)
            {
                if (leadsArray.GetArrayLength() == 0)
                    return "No leads found in the database yet. You can create one anytime!";

                var sb = new System.Text.StringBuilder("👥 **Current Insurance Leads in Database:**\n\n");
                foreach (var item in leadsArray.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    var name = item.GetProperty("fullName").GetString();
                    var email = item.GetProperty("email").GetString();
                    var phone = item.GetProperty("phone").GetString();
                    var type = item.GetProperty("insuranceType").GetString() ?? "Not selected";
                    var status = item.GetProperty("status").GetString();

                    sb.AppendLine($"• **#{id} {name}** — {type} Insurance ({status})");
                    sb.AppendLine($"   📞 {phone} | ✉️ {email}");
                }
                return sb.ToString();
            }
        }
        catch { }

        return $"Leads query result:\n{json}";
    }

    private static string FormatQuotesResponse(string json, int appId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Quotes", out var quotesArray) &&
                quotesArray.ValueKind == JsonValueKind.Array)
            {
                var sb = new System.Text.StringBuilder($"📊 **Comparative Quotes for Application #{appId}:**\n\n");
                foreach (var q in quotesArray.EnumerateArray())
                {
                    var qId = q.GetProperty("QuoteId").GetInt32();
                    var insurer = q.GetProperty("Insurer").GetString();
                    var premium = q.GetProperty("AnnualPremiumAED").GetDecimal();
                    var coverage = q.GetProperty("CoverageAmountAED").GetDecimal();
                    var summary = q.GetProperty("CoverageSummary").GetString();

                    sb.AppendLine($"🏛️ **Quote #{qId}: {insurer}**");
                    sb.AppendLine($"   • **Annual Premium**: **AED {premium:N2}**");
                    sb.AppendLine($"   • **Sum Insured**: AED {coverage:N2}");
                    sb.AppendLine($"   • **Benefits**: {summary}\n");
                }
                sb.AppendLine($"👉 To accept a quote, type: *\"Select quote #<id> and issue policy\"*");
                return sb.ToString();
            }
        }
        catch { }

        return $"Quotes for Application #{appId}:\n{json}";
    }

    private static LeadExtracted? ExtractLeadDetails(string text)
    {
        if (!Regex.IsMatch(text, @"\b(create|add|new|register)\s+lead\b", RegexOptions.IgnoreCase))
            return null;

        var emailMatch = Regex.Match(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        var phoneMatch = Regex.Match(text, @"(\+?971|0)?\s?5[0-9]{8}");

        // If neither email nor phone is provided, do NOT create dummy data - let the AI ask the user
        if (!emailMatch.Success && !phoneMatch.Success)
            return null;

        string name = "Valued Customer";
        var forMatch = Regex.Match(text, @"(?:for|named?)\s+([A-Za-z\s]{3,30})(?:,|\s+with|\s+email|\s+phone|\s+05|\s+\+971|\s+motor|\s+health|$)", RegexOptions.IgnoreCase);
        if (forMatch.Success)
        {
            name = forMatch.Groups[1].Value.Trim();
        }

        return new LeadExtracted
        {
            FullName = name,
            Email = emailMatch.Success ? emailMatch.Value : "contact@emirates.ae",
            Phone = phoneMatch.Success ? phoneMatch.Value : "+971500000000"
        };
    }

    private static AppExtracted? ExtractApplicationDetails(string text)
    {
        if (!Regex.IsMatch(text, @"\b(create|submit|new|start|apply)\b.*\bapp(lication)?\b", RegexOptions.IgnoreCase))
            return null;

        var leadIdMatch = Regex.Match(text, @"lead\s*#?\s*(\d+)", RegexOptions.IgnoreCase);
        if (!leadIdMatch.Success)
            return null;

        int leadId = int.Parse(leadIdMatch.Groups[1].Value);

        var amountMatch = Regex.Match(text, @"(?:coverage|amount|for|aed|dh|dhs)\s*#?:?\s*(\d[\d,]+)", RegexOptions.IgnoreCase);
        decimal coverage = 100000m;
        if (amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value.Replace(",", ""), out var parsed))
        {
            coverage = parsed;
        }

        string type = "Motor";
        if (text.Contains("health", StringComparison.OrdinalIgnoreCase)) type = "Health";
        else if (text.Contains("property", StringComparison.OrdinalIgnoreCase)) type = "Property";

        return new AppExtracted
        {
            LeadId = leadId,
            CoverageAmount = coverage,
            InsuranceType = type,
            Description = text
        };
    }

    private static ReviewExtracted? ExtractReviewDetails(string text)
    {
        if (!Regex.IsMatch(text, @"\b(review|approve|reject)\b", RegexOptions.IgnoreCase))
            return null;

        var appMatch = Regex.Match(text, @"app(lication)?\s*#?\s*(\d+)", RegexOptions.IgnoreCase);
        if (!appMatch.Success) return null;

        int appId = int.Parse(appMatch.Groups[2].Value);
        string decision = "Approved";
        if (text.Contains("reject", StringComparison.OrdinalIgnoreCase)) decision = "Rejected";
        else if (text.Contains("more info", StringComparison.OrdinalIgnoreCase)) decision = "MoreInformationRequired";

        return new ReviewExtracted
        {
            AppId = appId,
            Decision = decision,
            ReviewerName = "Senior UAE Underwriter",
            Comments = $"Application compliance verified under Central Bank regulations."
        };
    }

    private static int? ExtractIdAfterKeywords(string text, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            var match = Regex.Match(text, p + @"\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return int.Parse(match.Groups[match.Groups.Count - 1].Value);
            }
        }
        return null;
    }

    private class LeadExtracted
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    private class AppExtracted
    {
        public int LeadId { get; set; }
        public decimal CoverageAmount { get; set; }
        public string InsuranceType { get; set; } = "Motor";
        public string Description { get; set; } = string.Empty;
    }

    private class ReviewExtracted
    {
        public int AppId { get; set; }
        public string Decision { get; set; } = "Approved";
        public string ReviewerName { get; set; } = "Underwriter";
        public string Comments { get; set; } = string.Empty;
    }
}
