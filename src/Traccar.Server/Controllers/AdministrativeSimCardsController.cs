using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "SimCards" routes, at the exact paths the Blazor
/// fleet-management frontend calls — distinct from <see cref="SimCardsController"/>'s
/// "api/simcards" convention, which nothing in this frontend actually calls.
/// <see cref="SimCard"/> is an exact field-for-field match with the frontend's own model, so no
/// projection is needed here.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/SimCards/simcards")]
public class AdministrativeSimCardsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet("{resellerId:int}")]
    public async Task<ActionResult<List<SimCard>>> Get(int resellerId) =>
        await db.SimCards.Where(s => s.ResellerId == resellerId).OrderBy(s => s.Id).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<SimCard>> Create(SimCard simCard)
    {
        db.SimCards.Add(simCard);
        await db.SaveChangesAsync();
        return simCard;
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SimCard>> Update(int id, SimCard simCard)
    {
        var existing = await db.SimCards.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }
        simCard.Id = id;
        db.Entry(existing).CurrentValues.SetValues(simCard);
        await db.SaveChangesAsync();
        return existing;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var simCard = await db.SimCards.FindAsync(id);
        if (simCard != null)
        {
            db.SimCards.Remove(simCard);
            await db.SaveChangesAsync();
        }
        return Ok();
    }
}
