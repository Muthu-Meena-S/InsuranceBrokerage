using System.ComponentModel.DataAnnotations;

namespace InsuranceBrokerage.Application.dto.Insurers;

public class UpdateInsurerRequest
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;
}