using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "Clients and Resellers" routes, at the exact paths
/// the Blazor fleet-management frontend's AdminClientsService calls (its route strings are
/// hardcoded, so these can't move to <see cref="ClientsController"/>'s "api/clients" convention
/// without also editing the frontend). Same underlying data as ClientsController — a "reseller"
/// is a <see cref="Client"/> row with <see cref="Client.ParentId"/> null — just wrapped/routed to
/// match what AdminClientsService expects.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/>, matching MockApi (which enforces no auth
/// at all on any route): MainLayout.razor/ClientSelector.razor call these during Blazor Server's
/// static prerender pass, before the stored JWT is available via ProtectedLocalStorage — an
/// [Authorize] gate here turns that into an unhandled 401 that crashes the whole circuit.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/Clients/client")]
public class AdministrativeClientsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClientsResponse>> Get([FromQuery] int? parentId)
    {
        var query = parentId is > 0
            ? db.Clients.Where(c => c.ParentId == parentId)
            : db.Clients.Where(c => c.ParentId == null);
        var clients = await query.OrderBy(c => c.Id).ToListAsync();
        return new ClientsResponse { ClientViewModel = clients };
    }

    [HttpGet("inclusive")]
    public async Task<ActionResult<List<ClientInclusive>>> GetInclusive([FromQuery] int? parentId)
    {
        var query = parentId is > 0
            ? db.Clients.Where(c => c.ParentId == parentId)
            : db.Clients.Where(c => c.ParentId == null);
        var clients = await query.OrderBy(c => c.Id).ToListAsync();

        var result = new List<ClientInclusive>();
        foreach (var c in clients)
        {
            var devices = await db.Devices.Where(d => d.ClientId == c.Id).ToListAsync();
            result.Add(new ClientInclusive
            {
                Id = c.Id,
                Name = c.Name,
                IsDefault = c.IsDefault,
                Devices = [.. devices.Select(d => new DeviceInclusive
                {
                    Identifier = d.UniqueId,
                    Asset = [new AssetInclusive { AssetId = (int)d.Id, Name = d.Name, PlateNumber = d.Name, IsDisAssociated = d.IsDisAssociated }],
                })],
            });
        }
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<Client>> Create(Client client)
    {
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client;
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Client>> Update(int id, Client client)
    {
        var existing = await db.Clients.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }
        client.Id = id;
        db.Entry(existing).CurrentValues.SetValues(client);
        await db.SaveChangesAsync();
        return existing;
    }

    /// <summary>Marks this client as the sole default among ALL clients (table-wide, not scoped
    /// to siblings) — matches MockApi's "/mark/{id}" behaviour.</summary>
    [HttpPut("mark/{id:int}")]
    public async Task<IActionResult> MarkDefault(int id)
    {
        await db.Clients.ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsDefault, c => c.Id == id));
        return Ok();
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
        return Ok();
    }
}

/// <summary>
/// Port of MockApi's "/administrative/v1/api/Resellers/reseller/{id}/branding" route — the one
/// client route the frontend's pre-auth login page calls, to theme itself for the reseller
/// named in its URL. Deliberately not the full Client record (no email/phone/address).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/Resellers/reseller")]
public class ResellersController(TraccarDbContext db) : ControllerBase
{
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
