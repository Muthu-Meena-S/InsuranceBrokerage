using InsuranceBrokerage.Domain.Entities;

namespace InsuranceBrokerage.Application.Interfaces;

public interface IPolicyRepository
{
    Task<Policy> CreateAsync(Policy policy);
    Task<List<Policy>> GetAllAsync();
    Task<Policy?> GetByIdAsync(int id);
    Task<Policy> UpdateAsync(Policy policy);
    Task<bool> DeleteAsync(int id);
}

