using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Infrastructure.Repositories;

public class LeadRepository : ILeadRepository
{
    private readonly BrokerageDbContext _db;

    public LeadRepository(BrokerageDbContext db)
    {
        _db = db;
    }

    public async Task<Lead> CreateAsync(Lead lead)
    {
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return lead;
    }

    public async Task<List<Lead>> GetAllAsync()
    {
        return await _db.Leads
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Lead?> GetByIdAsync(int id)
    {
        return await _db.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<Lead> UpdateAsync(Lead lead)
    {
        _db.Leads.Update(lead);
        await _db.SaveChangesAsync();
        return lead;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var lead = await _db.Leads.FindAsync(id);
        if (lead is null)
            return false;

        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync();
        return true;
    }
}

