using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Domain.Entities;

public class Quote
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public int InsurerId { get; set; }

    public decimal Premium { get; set; }

    public decimal CoverageAmount { get; set; }

    public string Currency { get; set; } = "AED";

    public string CoverageSummary { get; set; } = string.Empty;

    public DateTime ValidUntilUtc { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Proposed;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public InsuranceApplication Application { get; set; } = null!;

    public Insurer Insurer { get; set; } = null!;

    public Policy? Policy { get; set; }
}