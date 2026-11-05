using InsuranceBrokerage.Domain.Entities;
using System;

public class Lead
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? InsuranceType { get; set; }

    public string Status { get; set; } = "New";

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public ICollection<InsuranceApplication> Applications { get; set; }
       = new List<InsuranceApplication>();
}
