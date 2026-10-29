using InsuranceBrokerage.AI.Models;

namespace InsuranceBrokerage.AI.Services;

public interface IAiOrchestrator
{
    Task<ChatResponse> ProcessMessageAsync(ChatRequest request);
}

