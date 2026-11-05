namespace InsuranceBrokerage.Application.dto.Insurers;

public class InsurerResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}