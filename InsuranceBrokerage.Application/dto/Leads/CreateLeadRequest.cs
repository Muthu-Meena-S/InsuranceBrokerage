using System.ComponentModel.DataAnnotations;

namespace InsuranceBrokerage.Application.dto.Leads;

public class CreateLeadRequest
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [RegularExpression("Motor|Health|Property")]
    public string? InsuranceType { get; set; }
}