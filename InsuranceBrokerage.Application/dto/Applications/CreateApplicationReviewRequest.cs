using System.ComponentModel.DataAnnotations;
using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Application.dto.Applications;

public class CreateApplicationReviewRequest
{
    [Range(1, int.MaxValue)]
    public int ApplicationId { get; set; }

    [Required]
    [StringLength(100)]
    public string ReviewerName { get; set; } = string.Empty;

    [EnumDataType(typeof(ReviewDecision))]
    public ReviewDecision Decision { get; set; }

    [StringLength(2000)]
    public string? Comments { get; set; }
}