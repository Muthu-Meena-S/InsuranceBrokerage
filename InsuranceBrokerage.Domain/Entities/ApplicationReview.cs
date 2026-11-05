using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Domain.Entities;

public class ApplicationReview
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public string ReviewerName { get; set; } = string.Empty;

    public ReviewDecision Decision { get; set; }

    public string? Comments { get; set; }

    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;

    public InsuranceApplication Application { get; set; } = null!;
}