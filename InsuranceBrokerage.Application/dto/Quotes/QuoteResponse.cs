using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Application.dto.Quotes;

public class QuoteResponse
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public int InsurerId { get; set; }

    public string? InsurerName { get; set; }

    public decimal Premium { get; set; }

    public decimal CoverageAmount { get; set; }

    public string Currency { get; set; } = "AED";

    public string CoverageSummary { get; set; } = string.Empty;

    public DateTime ValidUntilUtc { get; set; }

    public QuoteStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}