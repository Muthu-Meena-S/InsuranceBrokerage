using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Application.dto.Quotes;
using InsuranceBrokerage.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InsuranceBrokerage.Application.Services;

public class QuoteService : IQuoteService
{
    private readonly IQuoteRepository _repository;

    public QuoteService(IQuoteRepository repository)
    {
        _repository = repository;
    }

    public async Task<QuoteResponse> CreateQuoteAsync(CreateQuoteRequest request)
    {
        var quote = new Quote
        {
            ApplicationId = request.ApplicationId,
            InsurerId = request.InsurerId,
            Premium = request.Premium,
            CoverageAmount = request.CoverageAmount,
            Currency = request.Currency,
            CoverageSummary = request.CoverageSummary,
            ValidUntilUtc = request.ValidUntilUtc,
            Status = QuoteStatus.Proposed,
            CreatedAtUtc = DateTime.UtcNow
        };
        var created = await _repository.CreateAsync(quote);
        return MapToResponse(created);
    }

    public async Task<List<QuoteResponse>> GetAllQuotesAsync()
    {
        var quotes = await _repository.GetAllAsync();
        return quotes.Select(MapToResponse).ToList();
    }

    public async Task<List<QuoteResponse>> GetQuotesByApplicationIdAsync(int applicationId)
    {
        var quotes = await _repository.GetByApplicationIdAsync(applicationId);
        return quotes.Select(MapToResponse).ToList();
    }

    public async Task<QuoteResponse?> GetQuoteByIdAsync(int id)
    {
        var quote = await _repository.GetByIdAsync(id);
        return quote == null ? null : MapToResponse(quote);
    }

    public async Task<QuoteResponse?> UpdateQuoteAsync(int id, UpdateQuoteRequest request)
    {
        var quote = await _repository.GetByIdAsync(id);
        if (quote == null) return null;
        if (request.Premium != null) quote.Premium = request.Premium.Value;
        if (request.CoverageAmount != null) quote.CoverageAmount = request.CoverageAmount.Value;
        if (request.Currency != null) quote.Currency = request.Currency;
        if (request.CoverageSummary != null) quote.CoverageSummary = request.CoverageSummary;
        if (request.ValidUntilUtc != null) quote.ValidUntilUtc = request.ValidUntilUtc.Value;
        var updated = await _repository.UpdateAsync(quote);
        return MapToResponse(updated);
    }

    public async Task<bool> DeleteQuoteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    private static QuoteResponse MapToResponse(Quote quote)
    {
        return new QuoteResponse
        {
            Id = quote.Id,
            ApplicationId = quote.ApplicationId,
            InsurerId = quote.InsurerId,
            InsurerName = quote.Insurer?.Name,
            Premium = quote.Premium,
            CoverageAmount = quote.CoverageAmount,
            Currency = quote.Currency,
            CoverageSummary = quote.CoverageSummary,
            IsSelected = quote.IsSelected,
            ProposedAt = quote.ProposedAt,
            ValidUntilUtc = quote.ValidUntilUtc,
            Status = quote.Status.ToString()
        };
    }
}
