namespace InsuranceBrokerage.Domain.Entities;

public class Insurer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Quote> Quotes { get; set; }
        = new List<Quote>();
}