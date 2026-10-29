namespace InsuranceBrokerage.AI.Models;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage> ConversationHistory { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } = "user"; // "user", "assistant", "system"
    public string Content { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public List<ToolExecutionBadge> ToolsExecuted { get; set; } = new();
    public object? StructuredData { get; set; }
}

public class ToolExecutionBadge
{
    public string ToolName { get; set; } = string.Empty;
    public object? Arguments { get; set; }
    public string Summary { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
}

