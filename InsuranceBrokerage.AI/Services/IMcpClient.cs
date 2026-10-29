using System.Text.Json;
using InsuranceBrokerage.AI.Models;

namespace InsuranceBrokerage.AI.Services;

public interface IMcpClient
{
    Task<List<McpToolDefinition>> GetToolsAsync();
    Task<(bool Success, string Content)> CallToolAsync(string toolName, Dictionary<string, object?> arguments);
}

