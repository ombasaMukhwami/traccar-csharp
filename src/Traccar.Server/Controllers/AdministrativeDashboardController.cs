using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "Dashboard/stats" route, at the exact path the
/// Blazor fleet-management frontend calls. Unlike MockApi's synthetic
/// "speed &gt; 0 || ignition" heuristic, this reads the real <see cref="Device.Status"/> the
/// protocol pipeline's connection manager maintains from actual device connections.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/Dashboard")]
public class AdministrativeDashboardController(TraccarDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<List<DashboardStat>>> GetStats([FromQuery] int? resellerId)
    {
        var query = resellerId is > 0
            ? db.Clients.Where(c => c.ParentId == resellerId)
            : db.Clients.Where(c => c.ParentId != null);
        var clients = await query.OrderBy(c => c.Id).ToListAsync();

        var result = new List<DashboardStat>();
        foreach (var c in clients)
        {
            var online = await db.Devices.CountAsync(d => d.ClientId == c.Id && d.Status == Device.StatusOnline);
            var offline = await db.Devices.CountAsync(d => d.ClientId == c.Id && d.Status != Device.StatusOnline);
            result.Add(new DashboardStat { Id = c.Id, Name = c.Name, Online = online, Offline = offline });
        }
        return result;
    }
}
