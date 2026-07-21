using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>Port of MockApi's AdministrativeEndpoints "SimCards" routes.</summary>
[ApiController]
[Authorize]
[Route("api/simcards")]
public class SimCardsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SimCard>>> Get([FromQuery] int resellerId) =>
        await db.SimCards.Where(s => s.ResellerId == resellerId).OrderBy(s => s.Id).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SimCard>> GetById(int id)
    {
        var simCard = await db.SimCards.FindAsync(id);
        return simCard == null ? NotFound() : simCard;
    }

    [HttpPost]
    public async Task<ActionResult<SimCard>> Create(SimCard simCard)
    {
        db.SimCards.Add(simCard);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = simCard.Id }, simCard);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SimCard simCard)
    {
        var existing = await db.SimCards.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }
        simCard.Id = id;
        db.Entry(existing).CurrentValues.SetValues(simCard);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var simCard = await db.SimCards.FindAsync(id);
        if (simCard == null)
        {
            return NotFound();
        }
        db.SimCards.Remove(simCard);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
