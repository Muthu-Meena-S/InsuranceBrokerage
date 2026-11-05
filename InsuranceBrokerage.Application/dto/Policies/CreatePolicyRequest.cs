using System.ComponentModel.DataAnnotations;

namespace InsuranceBrokerage.Application.dto.Policies;

public class CreatePolicyRequest
{
    [Range(1, int.MaxValue)]
    public int ApplicationId { get; set; }

    [Range(1, int.MaxValue)]
    public int QuoteId { get; set; }

    [Required]
    [StringLength(100)]
    public string PolicyNumber { get; set; } = string.Empty;

    public DateTime? StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }
}