using System;
using System.Collections.Generic;
using System.Text;

namespace InsuranceBrokerage.Application.Interfaces
{
    public interface IQuoteService
{
    Task<QuoteResponse> CreateQuoteAsync(CreateQuoteRequest request);
    Task<List<QuoteResponse>> GetAllQuotesAsync();
    Task<List<QuoteResponse>> GetQuotesByApplicationIdAsync(int applicationId);
    Task<QuoteResponse?> GetQuoteByIdAsync(int id);
    Task<QuoteResponse?> UpdateQuoteAsync(int id, UpdateQuoteRequest request);
    Task<bool> DeleteQuoteAsync(int id);
}
    {
    }
}
