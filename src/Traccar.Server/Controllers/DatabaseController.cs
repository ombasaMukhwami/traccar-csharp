using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Traccar.Model;
using Traccar.Protocols;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>Traccar-style route onto <see cref="DatabaseStatsService"/> — see
/// <see cref="AdministrativeDatabaseController"/> for the Blazor fleet-management frontend's own
/// route onto the same data.</summary>
[ApiController]
[Authorize(Roles = ConfigKeys.Auth.RoleAdministrator)]
[Route("api/database")]
public class DatabaseController(TraccarDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DatabaseStats>> GetStats() => await DatabaseStatsService.GetStatsAsync(db);
}
