using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Infrastructure.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly BrokerageDbContext _db;

    public QuoteRepository(BrokerageDbContext db)
    {
        _db = db;
    }

    public async Task<Quote> CreateAsync(Quote quote)
    {
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync();
        return quote;
    }

    public async Task<List<Quote>> GetAllAsync()
    {
        return await _db.Quotes
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Quote>> GetByApplicationIdAsync(int applicationId)
    {
        return await _db.Quotes
            .AsNoTracking()
            .Where(q => q.ApplicationId == applicationId)
            .ToListAsync();
    }

    public async Task<Quote?> GetByIdAsync(int id)
    {
        return await _db.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<Quote> UpdateAsync(Quote quote)
    {
        _db.Quotes.Update(quote);
        await _db.SaveChangesAsync();
        return quote;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var quote = await _db.Quotes.FindAsync(id);
        if (quote == null) return false;
        _db.Quotes.Remove(quote);
        await _db.SaveChangesAsync();
        return true;
    }
}

