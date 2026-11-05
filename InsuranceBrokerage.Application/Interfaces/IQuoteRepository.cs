using InsuranceBrokerage.Domain.Entities;

namespace InsuranceBrokerage.Application.Interfaces;

public interface IQuoteRepository
{
    Task<Quote> CreateAsync(Quote quote);
    Task<List<Quote>> GetAllAsync();
    Task<List<Quote>> GetByApplicationIdAsync(int applicationId);
    Task<Quote?> GetByIdAsync(int id);
    Task<Quote> UpdateAsync(Quote quote);
    Task<bool> DeleteAsync(int id);
}
