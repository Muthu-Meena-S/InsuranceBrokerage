using InsuranceBrokerage.Domain.Enums;

namespace InsuranceBrokerage.Domain.Entities;

public class InsuranceApplication
{
    public int Id { get; set; }

    public int LeadId { get; set; }

    public InsuranceType InsuranceType { get; set; }

    public decimal CoverageAmount { get; set; }

    public string Currency { get; set; } = "AED";

    public string? Description { get; set; }

    public ApplicationStatus Status { get; set; }
        = ApplicationStatus.Draft;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SubmittedAtUtc { get; set; }

    // Foreign-key navigation to Lead.
    public Lead? Lead { get; set; } = null!;

    public ICollection<ApplicationReview> Reviews { get; set; }
        = new List<ApplicationReview>();

    public ICollection<Quote> Quotes { get; set; }
        = new List<Quote>();

    public ICollection<Policy> Policies { get; set; }
        = new List<Policy>();
}