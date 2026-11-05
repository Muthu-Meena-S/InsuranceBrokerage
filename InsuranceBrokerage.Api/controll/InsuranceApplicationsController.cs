
using InsuranceBrokerage.Application.dto;
using InsuranceBrokerage.Application.dto.Applications;
using InsuranceBrokerage.Application.DTOs.Applications;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Domain.Enums;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Api.controll;

[ApiController]
[Route("api/[controller]")]
public class InsuranceApplicationsController : ControllerBase
{
    private readonly BrokerageDbContext _db;

    public InsuranceApplicationsController(BrokerageDbContext db)
    {
        _db = db;
    }

    // GET: api/InsuranceApplications
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InsuranceApplicationResponse>>>
        GetAll()
    {
        var applications = await _db.Applications
            .AsNoTracking()
            .Select(a => new InsuranceApplicationResponse
            {
                Id = a.Id,
                LeadId = a.LeadId,
                InsuranceType = a.InsuranceType,
                CoverageAmount = a.CoverageAmount,
                Currency = a.Currency,
                Description = a.Description,
                Status = a.Status,
                CreatedAtUtc = a.CreatedAtUtc,
                SubmittedAtUtc = a.SubmittedAtUtc
            })
            .ToListAsync();

        return Ok(applications);
    }

    // GET: api/InsuranceApplications/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InsuranceApplicationResponse>>
        GetById(int id)
    {
        var application = await _db.Applications
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new InsuranceApplicationResponse
            {
                Id = a.Id,
                LeadId = a.LeadId,
                InsuranceType = a.InsuranceType,
                CoverageAmount = a.CoverageAmount,
                Currency = a.Currency,
                Description = a.Description,
                Status = a.Status,
                CreatedAtUtc = a.CreatedAtUtc,
                SubmittedAtUtc = a.SubmittedAtUtc
            })
            .FirstOrDefaultAsync();

        if (application is null)
        {
            return NotFound(new
            {
                message = "Application not found."
            });
        }

        return Ok(application);
    }


    [HttpPost("{id}/submit")]
    public async Task<IActionResult> SubmitApplication(int id)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null)
        {
            return NotFound("Insurance application not found.");
        }

        if (application.Status != ApplicationStatus.Draft)
        {
            return Conflict(
                "Only draft applications can be submitted.");
        }

        application.Status = ApplicationStatus.Submitted;
        application.SubmittedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            application.Id,
            application.Status,
            application.SubmittedAtUtc
        });
    }

    // POST: api/InsuranceApplications
    [HttpPost]
    public async Task<ActionResult<InsuranceApplicationResponse>> Create(
        [FromBody] CreateInsuranceApplicationRequest request)
    {
        // Check whether the lead exists.
        var leadExists = await _db.Leads
            .AnyAsync(l => l.Id == request.LeadId);

        if (!leadExists)
        {
            return NotFound(new
            {
                message = "Lead not found."
            });
        }

        var application = new InsuranceApplication
        {
            LeadId = request.LeadId,
            InsuranceType = request.InsuranceType,
            CoverageAmount = request.CoverageAmount,
            Currency = request.Currency,
            Description = request.Description,
            Status = ApplicationStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Applications.Add(application);
        await _db.SaveChangesAsync();

        var response = new InsuranceApplicationResponse
        {
            Id = application.Id,
            LeadId = application.LeadId,
            InsuranceType = application.InsuranceType,
            CoverageAmount = application.CoverageAmount,
            Currency = application.Currency,
            Description = application.Description,
            Status = application.Status,
            CreatedAtUtc = application.CreatedAtUtc,
            SubmittedAtUtc = application.SubmittedAtUtc
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = application.Id },
            response);
    }

    // PUT: api/InsuranceApplications/1
    [HttpPut("{id:int}")]
    public async Task<ActionResult<InsuranceApplicationResponse>> Update(
        int id,
        [FromBody] UpdateInsuranceApplicationRequest request)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application is null)
        {
            return NotFound(new
            {
                message = "Application not found."
            });
        }

        // Only update fields allowed by the update DTO.
        application.InsuranceType = request.InsuranceType;
        application.CoverageAmount = request.CoverageAmount;
        application.Currency = request.Currency;
        application.Description = request.Description;

        await _db.SaveChangesAsync();

        var response = new InsuranceApplicationResponse
        {
            Id = application.Id,
            LeadId = application.LeadId,
            InsuranceType = application.InsuranceType,
            CoverageAmount = application.CoverageAmount,
            Currency = application.Currency,
            Description = application.Description,
            Status = application.Status,
            CreatedAtUtc = application.CreatedAtUtc,
            SubmittedAtUtc = application.SubmittedAtUtc
        };

        return Ok(response);
    }

    // DELETE: api/InsuranceApplications/1
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application is null)
        {
            return NotFound(new
            {
                message = "Application not found."
            });
        }

        // Your DbContext restricts deletion when related records exist.
        var hasReviews = await _db.ApplicationReviews
            .AnyAsync(r => r.ApplicationId == id);

        var hasQuotes = await _db.Quotes
            .AnyAsync(q => q.ApplicationId == id);

        var hasPolicies = await _db.Policies
            .AnyAsync(p => p.ApplicationId == id);

        if (hasReviews || hasQuotes || hasPolicies)
        {
            return Conflict(new
            {
                message =
                    "This application cannot be deleted because it has related reviews, quotes, or policies."
            });
        }

        _db.Applications.Remove(application);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
