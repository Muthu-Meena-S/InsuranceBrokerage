using InsuranceBrokerage.Application.dto.Leads;
using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Domain.Enums;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace InsuranceBrokerage.McpServer.Tools;

[McpServerToolType]
public class InsuranceTools
{
    private readonly ILeadService _leadService;
    private readonly BrokerageDbContext _db;

    public InsuranceTools(ILeadService leadService, BrokerageDbContext db)
    {
        _leadService = leadService;
        _db = db;
    }

    [McpServerTool]
    [Description("Returns the available insurance products in the UAE brokerage system compliant with Central Bank of UAE (CBUAE) and DHA/DoH regulations.")]
    public object GetInsuranceProducts()
    {
        return new[]
        {
            new
            {
                Name = "Motor Insurance (Unified UAE Policy)",
                InsuranceType = "Motor",
                Description = "Covers comprehensive accidental damage, third-party liability (up to AED 2-5M), off-road coverage, agency repairs, and 24/7 UAE roadside assistance in compliance with CBUAE regulations."
            },
            new
            {
                Name = "Health Insurance (DHA / DoH Compliant)",
                InsuranceType = "Health",
                Description = "Mandatory UAE medical coverage: Essential Benefits Plan (EBP) and enhanced corporate/individual tiers covering inpatient, outpatient, maternity, and pharmacy networks across UAE hospitals and clinics."
            },
            new
            {
                Name = "Property & Home Insurance",
                InsuranceType = "Property",
                Description = "Comprehensive protection for villas, apartments, and commercial buildings against fire, water leakage, natural perils, tenant liability, and home contents across the Emirates."
            }
        };
    }

    [McpServerTool]
    [Description("Creates a new customer lead in the UAE brokerage database. Requires fullName, email, and phone (UAE +971 format preferred). Insurance type is optional and is selected when the lead is converted into an application.")]
    public async Task<object> CreateLead(
        string fullName,
        string email,
        string phone,
        string? insuranceType = null)
    {
        string[] allowedTypes = { "Motor", "Health", "Property" };

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(phone))
        {
            return new
            {
                Success = false,
                Message = "Customer full name, email, and phone number are required to create a lead."
            };
        }

        if (!string.IsNullOrWhiteSpace(insuranceType) &&
            !allowedTypes.Contains(insuranceType, StringComparer.OrdinalIgnoreCase))
        {
            return new
            {
                Success = false,
                Message = "Insurance type must be one of: Motor, Health, or Property."
            };
        }

        var matchedType = string.IsNullOrWhiteSpace(insuranceType)
            ? null
            : allowedTypes.First(type => type.Equals(insuranceType, StringComparison.OrdinalIgnoreCase));

        var request = new CreateLeadRequest
        {
            FullName = fullName.Trim(),
            Email = email.Trim(),
            Phone = phone.Trim(),
            InsuranceType = matchedType
        };

        var lead = await _leadService.CreateLeadAsync(request);

        return new
        {
            Success = true,
            Message = $"Lead #{lead.Id} successfully created for customer {lead.FullName}.",
            LeadId = lead.Id,
            Lead = lead
        };
    }

    [McpServerTool]
    [Description("Returns all insurance leads currently recorded in the brokerage system.")]
    public async Task<object> GetLeads()
    {
        var leads = await _leadService.GetAllLeadsAsync();

        return new
        {
            Count = leads.Count,
            Leads = leads
        };
    }

    [McpServerTool]
    [Description("Submits a formal insurance application for an existing lead. Requires leadId, insuranceType (Motor, Health, Property), coverageAmount in AED, and a brief description (e.g., vehicle make/model/year or medical history).")]
    public async Task<object> CreateApplication(
        int leadId,
        string insuranceType,
        decimal coverageAmount,
        string description)
    {
        var lead = await _db.Leads.FindAsync(leadId);
        if (lead is null)
        {
            return new
            {
                Success = false,
                Message = $"Lead with ID #{leadId} does not exist. Please create a lead first."
            };
        }

        if (!Enum.TryParse<InsuranceType>(insuranceType, true, out var parsedType))
        {
            return new
            {
                Success = false,
                Message = "Invalid insurance type. Allowed types are: Motor, Health, Property."
            };
        }

        if (coverageAmount <= 0)
        {
            return new
            {
                Success = false,
                Message = "Coverage amount must be greater than zero AED."
            };
        }

        var application = new InsuranceApplication
        {
            LeadId = leadId,
            InsuranceType = parsedType,
            CoverageAmount = coverageAmount,
            Currency = "AED",
            Description = description,
            Status = ApplicationStatus.Submitted,
            CreatedAtUtc = DateTime.UtcNow,
            SubmittedAtUtc = DateTime.UtcNow
        };

        _db.Applications.Add(application);
        await _db.SaveChangesAsync();

        return new
        {
            Success = true,
            Message = $"Application #{application.Id} submitted for {lead.FullName} with coverage of AED {coverageAmount:N2}.",
            ApplicationId = application.Id,
            InsuranceType = parsedType.ToString(),
            CoverageAmount = coverageAmount,
            Currency = "AED",
            Status = application.Status.ToString()
        };
    }

    [McpServerTool]
    [Description("Reviews an existing insurance application. Reviewer approves, rejects, or requests more information. Decision must be 'Approved', 'Rejected', or 'MoreInformationRequired'.")]
    public async Task<object> ReviewApplication(
        int applicationId,
        string reviewerName,
        string decision,
        string? comments)
    {
        var application = await _db.Applications
            .Include(a => a.Lead)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null)
        {
            return new
            {
                Success = false,
                Message = $"Application #{applicationId} not found."
            };
        }

        if (!Enum.TryParse<ReviewDecision>(decision, true, out var parsedDecision))
        {
            return new
            {
                Success = false,
                Message = "Decision must be 'Approved', 'Rejected', or 'MoreInformationRequired'."
            };
        }

        var review = new ApplicationReview
        {
            ApplicationId = applicationId,
            ReviewerName = string.IsNullOrWhiteSpace(reviewerName) ? "Underwriting Team" : reviewerName.Trim(),
            Decision = parsedDecision,
            Comments = comments ?? $"Application reviewed and marked as {parsedDecision}.",
            ReviewedAtUtc = DateTime.UtcNow
        };

        _db.ApplicationReviews.Add(review);

        // Update application status accordingly
        application.Status = parsedDecision switch
        {
            ReviewDecision.Approved => ApplicationStatus.Approved,
            ReviewDecision.Rejected => ApplicationStatus.Rejected,
            _ => ApplicationStatus.MoreInformationRequired
        };

        await _db.SaveChangesAsync();

        return new
        {
            Success = true,
            Message = $"Application #{applicationId} review recorded: {parsedDecision}.",
            ApplicationId = applicationId,
            ApplicantName = application.Lead?.FullName,
            NewStatus = application.Status.ToString(),
            Comments = review.Comments
        };
    }

    [McpServerTool]
    [Description("Generates and returns competitive insurance quotes from top UAE insurers (Sukoon, ADNIC, Orient) for an application. Application must exist.")]
    public async Task<object> ProposeQuotes(int applicationId)
    {
        var application = await _db.Applications
            .Include(a => a.Lead)
            .Include(a => a.Quotes)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null)
        {
            return new
            {
                Success = false,
                Message = $"Application #{applicationId} not found."
            };
        }

        // Ensure default UAE insurers exist in the database
        await EnsureDefaultInsurersAsync();

        var insurers = await _db.Insurers.Where(i => i.IsActive).ToListAsync();
        if (insurers.Count == 0)
        {
            return new
            {
                Success = false,
                Message = "No active UAE insurers configured."
            };
        }

        // Generate quotes tailored to UAE insurance formulas
        var generatedQuotes = new List<Quote>();

        foreach (var insurer in insurers.Take(3))
        {
            decimal rate = insurer.Name.Contains("Sukoon", StringComparison.OrdinalIgnoreCase) ? 0.048m :
                           insurer.Name.Contains("ADNIC", StringComparison.OrdinalIgnoreCase) ? 0.042m : 0.038m;

            decimal premium = Math.Round(application.CoverageAmount * rate, 2);
            if (application.InsuranceType == InsuranceType.Motor && premium < 1200) premium = 1200;
            if (application.InsuranceType == InsuranceType.Health && premium < 2500) premium = 2500;
            if (application.InsuranceType == InsuranceType.Property && premium < 650) premium = 650;

            string summary = application.InsuranceType switch
            {
                InsuranceType.Motor => $"{insurer.Name} Comprehensive: Agency repair, 24/7 UAE roadside assistance, emergency medical expenses, zero deductible on glass.",
                InsuranceType.Health => $"{insurer.Name} Enhanced Health: Comprehensive UAE hospital network, prescription medications 90% covered, dental & optical rider.",
                _ => $"{insurer.Name} Property Shield: Full structural and contents coverage against fire, floods, third-party tenant liability."
            };

            var quote = new Quote
            {
                ApplicationId = applicationId,
                InsurerId = insurer.Id,
                Premium = premium,
                CoverageAmount = application.CoverageAmount,
                Currency = "AED",
                CoverageSummary = summary,
                ValidUntilUtc = DateTime.UtcNow.AddDays(30),
                Status = QuoteStatus.Proposed,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Quotes.Add(quote);
            generatedQuotes.Add(quote);
        }

        await _db.SaveChangesAsync();

        return new
        {
            Success = true,
            ApplicationId = applicationId,
            ApplicantName = application.Lead?.FullName,
            InsuranceType = application.InsuranceType.ToString(),
            QuotesCount = generatedQuotes.Count,
            Quotes = generatedQuotes.Select(q => new
            {
                QuoteId = q.Id,
                Insurer = insurers.First(i => i.Id == q.InsurerId).Name,
                AnnualPremiumAED = q.Premium,
                CoverageAmountAED = q.CoverageAmount,
                CoverageSummary = q.CoverageSummary,
                ValidUntil = q.ValidUntilUtc.ToString("yyyy-MM-dd")
            })
        };
    }

    [McpServerTool]
    [Description("Selects a proposed quote and formally issues an active UAE insurance policy. Generates a unique UAE policy number (e.g. POL-UAE-YYYYMMDD-XXXX).")]
    public async Task<object> SelectQuoteAndIssuePolicy(int quoteId)
    {
        var quote = await _db.Quotes
            .Include(q => q.Application)
                .ThenInclude(a => a.Lead)
            .Include(q => q.Insurer)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote is null)
        {
            return new
            {
                Success = false,
                Message = $"Quote #{quoteId} not found."
            };
        }

        // Mark quote as Selected
        quote.Status = QuoteStatus.Selected;

        // Generate official policy number
        string policyNumber = $"POL-UAE-{DateTime.UtcNow:yyyyMMdd}-{quote.Id:D4}";

        var policy = new Policy
        {
            ApplicationId = quote.ApplicationId,
            QuoteId = quote.Id,
            PolicyNumber = policyNumber,
            Status = PolicyStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            StartDateUtc = DateTime.UtcNow,
            EndDateUtc = DateTime.UtcNow.AddYears(1)
        };

        _db.Policies.Add(policy);

        // Mark application as Approved/Active
        quote.Application.Status = ApplicationStatus.Approved;

        await _db.SaveChangesAsync();

        return new
        {
            Success = true,
            Message = $"Policy successfully issued with Policy Number: {policyNumber}",
            PolicyId = policy.Id,
            PolicyNumber = policyNumber,
            Insurer = quote.Insurer.Name,
            PolicyHolder = quote.Application.Lead?.FullName,
            InsuranceType = quote.Application.InsuranceType.ToString(),
            AnnualPremiumAED = quote.Premium,
            CoverageAmountAED = quote.CoverageAmount,
            ValidFrom = policy.StartDateUtc?.ToString("yyyy-MM-dd"),
            ValidTo = policy.EndDateUtc?.ToString("yyyy-MM-dd"),
            Status = policy.Status.ToString()
        };
    }

    [McpServerTool]
    [Description("Retrieves the full status and history of an insurance application, including reviews, quotes, and issued policies.")]
    public async Task<object> GetApplicationDetails(int applicationId)
    {
        var app = await _db.Applications
            .Include(a => a.Lead)
            .Include(a => a.Reviews)
            .Include(a => a.Quotes)
                .ThenInclude(q => q.Insurer)
            .Include(a => a.Policies)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app is null)
        {
            return new
            {
                Success = false,
                Message = $"Application #{applicationId} not found."
            };
        }

        return new
        {
            Success = true,
            ApplicationId = app.Id,
            LeadId = app.LeadId,
            CustomerName = app.Lead?.FullName,
            Email = app.Lead?.Email,
            Phone = app.Lead?.Phone,
            InsuranceType = app.InsuranceType.ToString(),
            CoverageAmountAED = app.CoverageAmount,
            Status = app.Status.ToString(),
            Description = app.Description,
            SubmittedAt = app.SubmittedAtUtc?.ToString("yyyy-MM-dd HH:mm"),
            Reviews = app.Reviews.Select(r => new
            {
                r.ReviewerName,
                Decision = r.Decision.ToString(),
                r.Comments,
                ReviewedAt = r.ReviewedAtUtc.ToString("yyyy-MM-dd")
            }),
            Quotes = app.Quotes.Select(q => new
            {
                q.Id,
                Insurer = q.Insurer?.Name,
                q.Premium,
                q.CoverageSummary,
                Status = q.Status.ToString()
            }),
            Policies = app.Policies.Select(p => new
            {
                p.Id,
                p.PolicyNumber,
                Status = p.Status.ToString(),
                StartDate = p.StartDateUtc?.ToString("yyyy-MM-dd"),
                EndDate = p.EndDateUtc?.ToString("yyyy-MM-dd")
            })
        };
    }

    private async Task EnsureDefaultInsurersAsync()
    {
        if (!await _db.Insurers.AnyAsync())
        {
            _db.Insurers.AddRange(
                new Insurer { Name = "Sukoon Insurance (Oman Insurance PJSC)", IsActive = true },
                new Insurer { Name = "ADNIC (Abu Dhabi National Insurance Company)", IsActive = true },
                new Insurer { Name = "Orient Insurance PJSC", IsActive = true },
                new Insurer { Name = "Daman National Health Insurance", IsActive = true }
            );
            await _db.SaveChangesAsync();
        }
    }
}
