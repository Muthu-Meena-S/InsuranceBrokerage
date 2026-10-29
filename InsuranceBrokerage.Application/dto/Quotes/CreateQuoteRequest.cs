using System.ComponentModel.DataAnnotations;

namespace InsuranceBrokerage.Application.dto.Quotes;

public class CreateQuoteRequest
{
    [Range(1, int.MaxValue)]
    public int ApplicationId { get; set; }

    [Range(1, int.MaxValue)]
    public int InsurerId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Premium { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal CoverageAmount { get; set; }

    [Required]
    [RegularExpression("AED|USD|EUR|GBP")]
    public string Currency { get; set; } = "AED";

    [Required]
    [StringLength(2000)]
    public string CoverageSummary { get; set; } = string.Empty;

    public DateTime ValidUntilUtc { get; set; }
}