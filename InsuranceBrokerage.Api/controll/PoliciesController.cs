
using InsuranceBrokerage.Application.dto.Policies;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Domain.Enums;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Api.controll;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly BrokerageDbContext _db;

    public PoliciesController(BrokerageDbContext db)
    {
        _db = db;
    }

    // GET: api/Policies
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PolicyResponse>>> GetAll()
    {
        var policies = await _db.Policies
            .AsNoTracking()
            .Select(p => new PolicyResponse
            {
                Id = p.Id,
                ApplicationId = p.ApplicationId,
                QuoteId = p.QuoteId,
                PolicyNumber = p.PolicyNumber,
                Status = p.Status,
                CreatedAtUtc = p.CreatedAtUtc,
                StartDateUtc = p.StartDateUtc,
                EndDateUtc = p.EndDateUtc
            })
            .ToListAsync();

        return Ok(policies);
    }

    // GET: api/Policies/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PolicyResponse>> GetById(int id)
    {
        var policy = await _db.Policies
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PolicyResponse
            {
                Id = p.Id,
                ApplicationId = p.ApplicationId,
                QuoteId = p.QuoteId,
                PolicyNumber = p.PolicyNumber,
                Status = p.Status,
                CreatedAtUtc = p.CreatedAtUtc,
                StartDateUtc = p.StartDateUtc,
                EndDateUtc = p.EndDateUtc
            })
            .FirstOrDefaultAsync();

        if (policy is null)
        {
            return NotFound(new { message = "Policy not found." });
        }

        return Ok(policy);
    }


    [HttpPost("{id}/submit-for-issuance")]
    public async Task<IActionResult> SubmitForIssuance(int id)
    {
        var policy = await _db.Policies
            .Include(p => p.Application)
            .Include(p => p.Quote)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
        {
            return NotFound(new { message = "Policy not found." });
        }

        if (policy.Status != PolicyStatus.Draft)
        {
            return Conflict(new
            {
                message = "Only draft policies can be submitted for issuance."
            });
        }

        if (policy.Application.Status != ApplicationStatus.Approved)
        {
            return Conflict(new
            {
                message = "The application must be approved first."
            });
        }

        if (policy.Quote.Status != QuoteStatus.Selected)
        {
            return Conflict(new
            {
                message = "The quote must be selected first."
            });
        }

        policy.Status = PolicyStatus.PendingIssuance;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            policy.Id,
            policy.PolicyNumber,
            policy.Status
        });
    }
    // POST: api/Policies
    [HttpPost]
    public async Task<ActionResult<PolicyResponse>> Create(
        [FromBody] CreatePolicyRequest request)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == request.ApplicationId);

        if (application is null)
        {
            return NotFound(new { message = "Application not found." });
        }

        var quote = await _db.Quotes
            .FirstOrDefaultAsync(q =>
                q.Id == request.QuoteId &&
                q.ApplicationId == request.ApplicationId);

        if (quote is null)
        {
            return BadRequest(new
            {
                message = "The quote does not belong to this application."
            });
        }

        if (application.Status != ApplicationStatus.Approved)
        {
            return Conflict(new
            {
                message =
                    "The application must be approved before policy creation."
            });
        }

        if (quote.Status != QuoteStatus.Selected)
        {
            return Conflict(new
            {
                message =
                    "The quote must be selected before policy creation."
            });
        }

        if (quote.ValidUntilUtc <= DateTime.UtcNow)
        {
            return Conflict(new
            {
                message = "The selected quote has expired."
            });
        }

        if (string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            return BadRequest(new
            {
                message = "Policy number is required."
            });
        }

        if (request.StartDateUtc.HasValue &&
            request.EndDateUtc.HasValue &&
            request.EndDateUtc.Value <= request.StartDateUtc.Value)
        {
            return BadRequest(new
            {
                message = "The end date must be later than the start date."
            });
        }

        var policyNumber = request.PolicyNumber.Trim();

        var duplicateNumber = await _db.Policies
            .AnyAsync(p => p.PolicyNumber == policyNumber);

        if (duplicateNumber)
        {
            return Conflict(new
            {
                message = "Policy number already exists."
            });
        }

        var policy = new Policy
        {
            ApplicationId = request.ApplicationId,
            QuoteId = request.QuoteId,
            PolicyNumber = policyNumber,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            Status = PolicyStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Policies.Add(policy);
        await _db.SaveChangesAsync();

        var response = new PolicyResponse
        {
            Id = policy.Id,
            ApplicationId = policy.ApplicationId,
            QuoteId = policy.QuoteId,
            PolicyNumber = policy.PolicyNumber,
            Status = policy.Status,
            CreatedAtUtc = policy.CreatedAtUtc,
            StartDateUtc = policy.StartDateUtc,
            EndDateUtc = policy.EndDateUtc
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = policy.Id },
            response);
    }
    [HttpPost("{id}/activate")]
    public async Task<IActionResult> ActivatePolicy(int id)
    {
        var policy = await _db.Policies
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
        {
            return NotFound(new { message = "Policy not found." });
        }

        if (policy.Status != PolicyStatus.PendingIssuance)
        {
            return Conflict(new
            {
                message = "Only policies pending issuance can be activated."
            });
        }

        if (!policy.StartDateUtc.HasValue ||
            !policy.EndDateUtc.HasValue)
        {
            return Conflict(new
            {
                message = "Policy start and end dates are required."
            });
        }

        if (policy.EndDateUtc.Value <= policy.StartDateUtc.Value)
        {
            return Conflict(new
            {
                message = "The policy end date must be after its start date."
            });
        }

        policy.Status = PolicyStatus.Active;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            policy.Id,
            policy.PolicyNumber,
            policy.Status,
            policy.StartDateUtc,
            policy.EndDateUtc
        });
    }
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelPolicy(int id)
    {
        var policy = await _db.Policies
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
        {
            return NotFound(new
            {
                message = "Policy not found."
            });
        }

        if (policy.Status == PolicyStatus.Cancelled ||
            policy.Status == PolicyStatus.Expired)
        {
            return Conflict(new
            {
                message = "The policy is already cancelled or expired."
            });
        }

        policy.Status = PolicyStatus.Cancelled;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            policy.Id,
            policy.PolicyNumber,
            policy.Status
        });
    }
    [HttpPost("{id}/expire")]
    public async Task<IActionResult> ExpirePolicy(int id)
    {
        var policy = await _db.Policies
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
        {
            return NotFound(new
            {
                message = "Policy not found."
            });
        }

        if (policy.Status != PolicyStatus.Active)
        {
            return Conflict(new
            {
                message = "Only active policies can expire."
            });
        }

        if (!policy.EndDateUtc.HasValue ||
            policy.EndDateUtc.Value > DateTime.UtcNow)
        {
            return Conflict(new
            {
                message = "The policy end date has not passed yet."
            });
        }

        policy.Status = PolicyStatus.Expired;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            policy.Id,
            policy.PolicyNumber,
            policy.Status
        });
    }
}
