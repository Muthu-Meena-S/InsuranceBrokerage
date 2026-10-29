using InsuranceBrokerage.Domain.Entities;

namespace InsuranceBrokerage.Application.Interfaces;

public interface ILeadRepository
{
    Task<Lead> CreateAsync(Lead lead);
    Task<List<Lead>> GetAllAsync();
    Task<Lead?> GetByIdAsync(int id);
    Task<Lead> UpdateAsync(Lead lead);
    Task<bool> DeleteAsync(int id);
}

