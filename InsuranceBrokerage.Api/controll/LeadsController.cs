using InsuranceBrokerage.Application.dto.Leads;
using InsuranceBrokerage.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceBrokerage.Api.controll;

[ApiController]
[Route("api/[controller]")]
public class LeadsController : ControllerBase
{
    private readonly ILeadService _leadService;

    public LeadsController(ILeadService leadService)
    {
        _leadService = leadService;
    }

    // GET: api/leads
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadResponse>>> GetAll()
    {
        var leads = await _leadService.GetAllLeadsAsync();
        return Ok(leads);
    }

    // GET: api/leads/1
    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadResponse>> GetById(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(id);
        if (lead is null)
        {
            return NotFound(new { message = "Lead not found." });
        }

        return Ok(lead);
    }

    // POST: api/leads
    [HttpPost]
    public async Task<ActionResult<LeadResponse>> Create([FromBody] CreateLeadRequest request)
    {
        var response = await _leadService.CreateLeadAsync(request);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);
    }

    // PUT: api/leads/1
    [HttpPut("{id:int}")]
    public async Task<ActionResult<LeadResponse>> Update(int id, [FromBody] UpdateLeadRequest request)
    {
        var response = await _leadService.UpdateLeadAsync(id, request);
        if (response is null)
        {
            return NotFound(new { message = "Lead not found." });
        }

        return Ok(response);
    }

    // DELETE: api/leads/1
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _leadService.DeleteLeadAsync(id);
        if (!deleted)
        {
            return NotFound(new { message = "Lead not found or cannot be deleted." });
        }

        return NoContent();
    }
}
