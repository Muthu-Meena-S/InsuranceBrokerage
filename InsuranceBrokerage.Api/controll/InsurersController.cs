
using InsuranceBrokerage.Application.dto.Insurers;
using InsuranceBrokerage.Domain.Entities;
using InsuranceBrokerage.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceBrokerage.Api.controll;

[ApiController]
[Route("api/[controller]")]
public class InsurersController : ControllerBase
{
    private readonly BrokerageDbContext _db;

    public InsurersController(BrokerageDbContext db)
    {
        _db = db;
    }

    // GET: api/Insurers
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InsurerResponse>>> GetAll()
    {
        var insurers = await _db.Insurers
            .AsNoTracking()
            .Select(i => new InsurerResponse
            {
                Id = i.Id,
                Name = i.Name,
                IsActive = i.IsActive
            })
            .ToListAsync();

        return Ok(insurers);
    }

    // GET: api/Insurers/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InsurerResponse>> GetById(int id)
    {
        var insurer = await _db.Insurers
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InsurerResponse
            {
                Id = i.Id,
                Name = i.Name,
                IsActive = i.IsActive
            })
            .FirstOrDefaultAsync();

        if (insurer is null)
        {
            return NotFound(new { message = "Insurer not found." });
        }

        return Ok(insurer);
    }

    // POST: api/Insurers
    [HttpPost]
    public async Task<ActionResult<InsurerResponse>> Create(
        [FromBody] CreateInsurerRequest request)
    {
        var name = request.Name.Trim();

        var alreadyExists = await _db.Insurers
            .AnyAsync(i => i.Name == name);

        if (alreadyExists)
        {
            return Conflict(new
            {
                message = "An insurer with this name already exists."
            });
        }

        var insurer = new Insurer
        {
            Name = name,
            IsActive = true
        };

        _db.Insurers.Add(insurer);
        await _db.SaveChangesAsync();

        var response = new InsurerResponse
        {
            Id = insurer.Id,
            Name = insurer.Name,
            IsActive = insurer.IsActive
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = insurer.Id },
            response);
    }
}
