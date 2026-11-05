using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Application.dto.Applications;

public class ApplicationReviewResponse
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public string ReviewerName { get; set; } = string.Empty;

    public ReviewDecision Decision { get; set; }

    public string? Comments { get; set; }

    public DateTime ReviewedAtUtc { get; set; }
}