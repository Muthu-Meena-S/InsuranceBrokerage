
using InsuranceBrokerage.Application.dto.Quotes;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Domain.Enums;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Api.controll;

[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly BrokerageDbContext _db;

    public QuotesController(BrokerageDbContext db)
    {
        _db = db;
    }

    // GET: api/Quotes
    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuoteResponse>>> GetAll()
    {
        var quotes = await _db.Quotes
            .AsNoTracking()
            .Select(q => new QuoteResponse
            {
                Id = q.Id,
                ApplicationId = q.ApplicationId,
                InsurerId = q.InsurerId,
                InsurerName = q.Insurer.Name,
                Premium = q.Premium,
                CoverageAmount = q.CoverageAmount,
                Currency = q.Currency,
                CoverageSummary = q.CoverageSummary,
                ValidUntilUtc = q.ValidUntilUtc,
                Status = q.Status,
                CreatedAtUtc = q.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(quotes);
    }

    // GET: api/Quotes/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuoteResponse>> GetById(int id)
    {
        var quote = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.Id == id)
            .Select(q => new QuoteResponse
            {
                Id = q.Id,
                ApplicationId = q.ApplicationId,
                InsurerId = q.InsurerId,
                InsurerName = q.Insurer.Name,
                Premium = q.Premium,
                CoverageAmount = q.CoverageAmount,
                Currency = q.Currency,
                CoverageSummary = q.CoverageSummary,
                ValidUntilUtc = q.ValidUntilUtc,
                Status = q.Status,
                CreatedAtUtc = q.CreatedAtUtc
            })
            .FirstOrDefaultAsync();

        if (quote is null)
        {
            return NotFound(new { message = "Quote not found." });
        }

        return Ok(quote);
    }
    [HttpPost("{id}/select")]
    public async Task<IActionResult> SelectQuote(int id)
    {
        var quote = await _db.Quotes
            .Include(q => q.Application)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quote == null)
        {
            return NotFound(new { message = "Quote not found." });
        }

        if (quote.Application.Status != ApplicationStatus.Approved)
        {
            return Conflict(new
            {
                message = "The application must be approved first."
            });
        }

        if (quote.Status != QuoteStatus.Proposed)
        {
            return Conflict(new
            {
                message = "Only proposed quotes can be selected."
            });
        }

        if (quote.ValidUntilUtc <= DateTime.UtcNow)
        {
            return Conflict(new { message = "The quote has expired." });
        }

        quote.Status = QuoteStatus.Selected;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            quote.Id,
            quote.Status
        });
    }
    // POST: api/Quotes
    [HttpPost]
    public async Task<ActionResult<QuoteResponse>> Create(
        [FromBody] CreateQuoteRequest request)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == request.ApplicationId);

        if (application is null)
        {
            return NotFound(new { message = "Application not found." });
        }

        var insurerIsActive = await _db.Insurers
            .AnyAsync(i => i.Id == request.InsurerId && i.IsActive);

        if (!insurerIsActive)
        {
            return BadRequest(new
            {
                message = "Active insurer not found."
            });
        }

        if (request.Premium <= 0 || request.CoverageAmount <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Premium and coverage amount must be greater than zero."
            });
        }

        if (request.ValidUntilUtc <= DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message = "The quote validity date must be in the future."
            });
        }

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

        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync();

        var response = new QuoteResponse
        {
            Id = quote.Id,
            ApplicationId = quote.ApplicationId,
            InsurerId = quote.InsurerId,
            InsurerName = await _db.Insurers
                .Where(i => i.Id == quote.InsurerId)
                .Select(i => i.Name)
                .FirstAsync(),
            Premium = quote.Premium,
            CoverageAmount = quote.CoverageAmount,
            Currency = quote.Currency,
            CoverageSummary = quote.CoverageSummary,
            ValidUntilUtc = quote.ValidUntilUtc,
            Status = quote.Status,
            CreatedAtUtc = quote.CreatedAtUtc
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = quote.Id },
            response);
    }
}
