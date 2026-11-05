using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Application.DTOs.Applications;

public class InsuranceApplicationResponse
{
    public int Id { get; set; }

    public int LeadId { get; set; }

    public InsuranceType InsuranceType { get; set; }

    public decimal CoverageAmount { get; set; }

    public string Currency { get; set; } = "AED";

    public string? Description { get; set; }

    public ApplicationStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
}