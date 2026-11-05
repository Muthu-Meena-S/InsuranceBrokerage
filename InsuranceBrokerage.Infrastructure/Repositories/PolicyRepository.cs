using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Infrastructure.Repositories;

public class PolicyRepository : IPolicyRepository
{
    private readonly BrokerageDbContext _db;

    public PolicyRepository(BrokerageDbContext db)
    {
        _db = db;
    }

    public async Task<Policy> CreateAsync(Policy policy)
    {
        _db.Policies.Add(policy);
        await _db.SaveChangesAsync();
        return policy;
    }

    public async Task<List<Policy>> GetAllAsync()
    {
        return await _db.Policies
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Policy?> GetByIdAsync(int id)
    {
        return await _db.Policies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Policy> UpdateAsync(Policy policy)
    {
        _db.Policies.Update(policy);
        await _db.SaveChangesAsync();
        return policy;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var policy = await _db.Policies.FindAsync(id);
        if (policy == null) return false;
        _db.Policies.Remove(policy);
        await _db.SaveChangesAsync();
        return true;
    }
}

