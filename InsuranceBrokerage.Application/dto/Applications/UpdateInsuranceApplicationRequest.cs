using System.ComponentModel.DataAnnotations;
using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Application.dto.Applications;

public class UpdateInsuranceApplicationRequest
{
    [EnumDataType(typeof(InsuranceType))]
    public InsuranceType InsuranceType { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal CoverageAmount { get; set; }

    [Required]
    [RegularExpression("AED|USD|EUR|GBP")]
    public string Currency { get; set; } = "AED";

    [StringLength(1000)]
    public string? Description { get; set; }
}