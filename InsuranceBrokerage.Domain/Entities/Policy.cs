using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Domain.Entities;

public class Policy
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public int QuoteId { get; set; }

    public string PolicyNumber { get; set; } = string.Empty;

    public PolicyStatus Status { get; set; } = PolicyStatus.Draft;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public Quote Quote { get; set; } = null!;
}