using InsuranceBrokerage.Domain.Entities;

namespace InsuranceBrokerage.Application.Interfaces;

public interface IApplicationRepository
{
    Task<InsuranceApplication> CreateAsync(InsuranceApplication application);
    Task<List<InsuranceApplication>> GetAllAsync();
    Task<InsuranceApplication?> GetByIdAsync(int id);
    Task<InsuranceApplication> UpdateAsync(InsuranceApplication application);
    Task<bool> DeleteAsync(int id);
}
