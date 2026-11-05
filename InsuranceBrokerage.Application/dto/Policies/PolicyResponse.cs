using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Application.dto.Policies;

public class PolicyResponse
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public int QuoteId { get; set; }

    public string PolicyNumber { get; set; } = string.Empty;

    public PolicyStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }
}