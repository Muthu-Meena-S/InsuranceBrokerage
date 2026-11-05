using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly BrokerageDbContext _db;

    public ApplicationRepository(BrokerageDbContext db)
    {
        _db = db;
    }

    public async Task<InsuranceApplication> CreateAsync(InsuranceApplication application)
    {
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();
        return application;
    }

    public async Task<List<InsuranceApplication>> GetAllAsync()
    {
        return await _db.Applications
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<InsuranceApplication?> GetByIdAsync(int id)
    {
        return await _db.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<InsuranceApplication> UpdateAsync(InsuranceApplication application)
    {
        _db.Applications.Update(application);
        await _db.SaveChangesAsync();
        return application;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var app = await _db.Applications.FindAsync(id);
        if (app == null) return false;
        _db.Applications.Remove(app);
        await _db.SaveChangesAsync();
        return true;
    }
}

