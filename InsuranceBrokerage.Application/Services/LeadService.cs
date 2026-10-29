using InsuranceBrokerage.Application.dto.Leads;
using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Domain.Entities;

namespace InsuranceBrokerage.Application.Services;

public class LeadService : ILeadService
{
    private readonly ILeadRepository _leadRepository;

    public LeadService(ILeadRepository leadRepository)
    {
        _leadRepository = leadRepository;
    }

    public async Task<LeadResponse> CreateLeadAsync(CreateLeadRequest request)
    {
        var lead = new Lead
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            InsuranceType = request.InsuranceType,
            Status = "New",
            CreatedAtUtc = DateTime.UtcNow
        };

        var createdLead = await _leadRepository.CreateAsync(lead);

        return MapToResponse(createdLead);
    }

    public async Task<List<LeadResponse>> GetAllLeadsAsync()
    {
        var leads = await _leadRepository.GetAllAsync();

        return leads.Select(MapToResponse).ToList();
    }

    public async Task<LeadResponse?> GetLeadByIdAsync(int id)
    {
        var lead = await _leadRepository.GetByIdAsync(id);

        return lead is null ? null : MapToResponse(lead);
    }

    public async Task<LeadResponse?> UpdateLeadAsync(int id, UpdateLeadRequest request)
    {
        var lead = await _leadRepository.GetByIdAsync(id);

        if (lead is null)
            return null;

        lead.FullName = request.FullName;
        lead.Email = request.Email;
        lead.Phone = request.Phone;
        lead.InsuranceType = request.InsuranceType;
        lead.Status = request.Status;

        var updatedLead = await _leadRepository.UpdateAsync(lead);

        return MapToResponse(updatedLead);
    }

    public Task<bool> DeleteLeadAsync(int id)
    {
        return _leadRepository.DeleteAsync(id);
    }

    private static LeadResponse MapToResponse(Lead lead)
    {
        return new LeadResponse
        {
            Id = lead.Id,
            FullName = lead.FullName,
            Email = lead.Email,
            Phone = lead.Phone,
            InsuranceType = lead.InsuranceType,
            Status = lead.Status,
            CreatedAtUtc = lead.CreatedAtUtc
        };
    }
}
