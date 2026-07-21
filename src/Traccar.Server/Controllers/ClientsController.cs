using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Reseller/client tenant management — port of MockApi's AccountEndpoints/AdministrativeEndpoints
/// "Clients and Resellers" routes. A root (<see cref="Client.ParentId"/> null) is a reseller; a
/// row with ParentId set is one of its clients.
/// </summary>
[ApiController]
[Authorize]
[Route("api/clients")]
public class ClientsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Client>>> Get([FromQuery] int? parentId)
    {
        var query = parentId is > 0
            ? db.Clients.Where(c => c.ParentId == parentId)
            : db.Clients.Where(c => c.ParentId == null);
        return await query.OrderBy(c => c.Id).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Client>> GetById(int id)
    {
        var client = await db.Clients.FindAsync(id);
        return client == null ? NotFound() : client;
    }

    [HttpPost]
    public async Task<ActionResult<Client>> Create(Client client)
    {
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Client client)
    {
        var existing = await db.Clients.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }
        client.Id = id;
        db.Entry(existing).CurrentValues.SetValues(client);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Marks this client as the sole default among ALL clients (table-wide, not
    /// scoped to siblings) — matches MockApi's "/mark/{id}" behaviour.</summary>
    [HttpPut("{id:int}/mark-default")]
    public async Task<IActionResult> MarkDefault(int id)
    {
        await db.Clients.ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsDefault, c => c.Id == id));
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null)
        {
            return NotFound();
        }
        db.Clients.Remove(client);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Just enough of a reseller to theme a pre-auth login page — the one client route
    /// callable without a session (see Login.razor's "/{ResellerId:int}" route in the Blazor app).</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}/branding")]
    public async Task<ActionResult<ResellerBranding>> GetBranding(int id)
    {
        var reseller = await db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.ParentId == null);
        if (reseller == null)
        {
            return NotFound();
        }
        return new ResellerBranding
        {
            Name = reseller.Name,
            PrimaryColor = reseller.PrimaryColor,
            SecondaryColor = reseller.SecondaryColor,
            MapProvider = reseller.MapProvider,
            MapboxAccessToken = reseller.MapboxAccessToken,
            GoogleMapsApiKey = reseller.GoogleMapsApiKey,
        };
    }
}
