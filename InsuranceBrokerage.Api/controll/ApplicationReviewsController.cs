
using InsuranceBrokerage.Application.dto.Applications;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Domain.Enums;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Api.controll;

[ApiController]
[Route("api/[controller]")]
public class ApplicationReviewsController : ControllerBase
{
    private readonly BrokerageDbContext _db;

    public ApplicationReviewsController(BrokerageDbContext db)
    {
        _db = db;
    }

    // GET: api/ApplicationReviews
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApplicationReviewResponse>>>
        GetAll()
    {
        var reviews = await _db.ApplicationReviews
            .AsNoTracking()
            .Select(r => new ApplicationReviewResponse
            {
                Id = r.Id,
                ApplicationId = r.ApplicationId,
                ReviewerName = r.ReviewerName,
                Decision = r.Decision,
                Comments = r.Comments,
                ReviewedAtUtc = r.ReviewedAtUtc
            })
            .ToListAsync();

        return Ok(reviews);
    }

    // GET: api/ApplicationReviews/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApplicationReviewResponse>>
        GetById(int id)
    {
        var review = await _db.ApplicationReviews
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ApplicationReviewResponse
            {
                Id = r.Id,
                ApplicationId = r.ApplicationId,
                ReviewerName = r.ReviewerName,
                Decision = r.Decision,
                Comments = r.Comments,
                ReviewedAtUtc = r.ReviewedAtUtc
            })
            .FirstOrDefaultAsync();

        if (review is null)
        {
            return NotFound(new { message = "Review not found." });
        }

        return Ok(review);
    }

    // GET: api/ApplicationReviews/by-application/1
    [HttpGet("by-application/{applicationId:int}")]
    public async Task<ActionResult<IEnumerable<ApplicationReviewResponse>>>
        GetByApplication(int applicationId)
    {
        var applicationExists = await _db.Applications
            .AnyAsync(a => a.Id == applicationId);

        if (!applicationExists)
        {
            return NotFound(new
            {
                message = "Application not found."
            });
        }

        var reviews = await _db.ApplicationReviews
            .AsNoTracking()
            .Where(r => r.ApplicationId == applicationId)
            .Select(r => new ApplicationReviewResponse
            {
                Id = r.Id,
                ApplicationId = r.ApplicationId,
                ReviewerName = r.ReviewerName,
                Decision = r.Decision,
                Comments = r.Comments,
                ReviewedAtUtc = r.ReviewedAtUtc
            })
            .ToListAsync();

        return Ok(reviews);
    }

    // POST: api/ApplicationReviews
    [HttpPost]
    public async Task<ActionResult<ApplicationReviewResponse>> Create(
        [FromBody] CreateApplicationReviewRequest request)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == request.ApplicationId);

        if (application is null)
        {
            return NotFound(new
            {
                message = "Application not found."
            });
        }

        // A submitted application must be reviewed.
        if (application.Status != ApplicationStatus.Submitted &&
            application.Status != ApplicationStatus.UnderReview)
        {
            return Conflict(new
            {
                message =
                    "Only submitted or under-review applications can be reviewed."
            });
        }

        // Convert the review decision to the application status.
        var newStatus = request.Decision switch
        {
            ReviewDecision.Approved =>
                ApplicationStatus.Approved,

            ReviewDecision.Rejected =>
                ApplicationStatus.Rejected,

            ReviewDecision.MoreInformationRequired =>
                ApplicationStatus.MoreInformationRequired,

            _ => (ApplicationStatus?)null
        };

        if (newStatus is null)
        {
            return BadRequest(new
            {
                message = "Invalid review decision."
            });
        }

        var review = new ApplicationReview
        {
            ApplicationId = request.ApplicationId,
            ReviewerName = request.ReviewerName,
            Decision = request.Decision,
            Comments = request.Comments,
            ReviewedAtUtc = DateTime.UtcNow
        };

        application.Status = newStatus.Value;

        _db.ApplicationReviews.Add(review);
        await _db.SaveChangesAsync();

        var response = new ApplicationReviewResponse
        {
            Id = review.Id,
            ApplicationId = review.ApplicationId,
            ReviewerName = review.ReviewerName,
            Decision = review.Decision,
            Comments = review.Comments,
            ReviewedAtUtc = review.ReviewedAtUtc
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = review.Id },
            response);
    }
}
