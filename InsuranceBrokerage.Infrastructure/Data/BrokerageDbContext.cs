
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Infrastructure.Data;

public class BrokerageDbContext : IdentityDbContext<ApplicationUser>
{
    public BrokerageDbContext(
        DbContextOptions<BrokerageDbContext> options)
        : base(options)
    {
    }

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<InsuranceApplication> Applications =>
        Set<InsuranceApplication>();

    public DbSet<ApplicationReview> ApplicationReviews =>
        Set<ApplicationReview>();

    public DbSet<Insurer> Insurers => Set<Insurer>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<Policy> Policies => Set<Policy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Lead>()
            .HasMany(l => l.Applications)
            .WithOne(a => a.Lead)
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InsuranceApplication>()
            .HasMany(a => a.Reviews)
            .WithOne(r => r.Application)
            .HasForeignKey(r => r.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InsuranceApplication>()
            .HasMany(a => a.Quotes)
            .WithOne(q => q.Application)
            .HasForeignKey(q => q.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InsuranceApplication>()
            .HasMany(a => a.Policies)
            .WithOne(p => p.Application)
            .HasForeignKey(p => p.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Quote>()
            .HasOne(q => q.Policy)
            .WithOne(p => p.Quote)
            .HasForeignKey<Policy>(p => p.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InsuranceApplication>()
            .Property(a => a.CoverageAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Quote>()
            .Property(q => q.CoverageAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Quote>()
            .Property(q => q.Premium)
            .HasPrecision(18, 2);
    }
}
