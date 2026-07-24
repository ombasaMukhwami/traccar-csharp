using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's TelemetryEndpoints "Geozones" routes — client-scoped map zones drawn/edited
/// on the live map (see LeafletMap.razor's draw callbacks).
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("telemetry/api/geozone/data")]
public class GeozonesController(TraccarDbContext db) : ControllerBase
{
    [HttpGet("{clientId:int}")]
    public async Task<ActionResult<List<Geozone>>> Get(int clientId) =>
        await db.Geozones.Where(g => g.ClientId == clientId).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<Geozone>> Create(Geozone geozone)
    {
        db.Geozones.Add(geozone);
        await db.SaveChangesAsync();
        return geozone;
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Geozone>> Update(int id, Geozone geozone)
    {
        var existing = await db.Geozones.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }
        geozone.Id = id;
        db.Entry(existing).CurrentValues.SetValues(geozone);
        await db.SaveChangesAsync();
        return existing;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await db.Geozones.FindAsync(id);
        if (existing != null)
        {
            db.Geozones.Remove(existing);
            await db.SaveChangesAsync();
        }
        return Ok();
    }
}
