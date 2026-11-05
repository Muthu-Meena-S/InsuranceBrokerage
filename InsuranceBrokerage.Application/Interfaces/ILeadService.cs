using InsuranceBrokerage.Application.dto.Leads;

namespace InsuranceBrokerage.Application.Interfaces;

public interface ILeadService
{
    Task<LeadResponse> CreateLeadAsync(CreateLeadRequest request);
    Task<List<LeadResponse>> GetAllLeadsAsync();
    Task<LeadResponse?> GetLeadByIdAsync(int id);
    Task<LeadResponse?> UpdateLeadAsync(int id, UpdateLeadRequest request);
    Task<bool> DeleteLeadAsync(int id);
}
