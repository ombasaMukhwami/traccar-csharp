using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's DatabaseStatsEndpoints, at the exact path the Blazor fleet-management
/// frontend calls.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/>, matching MockApi (which enforces no auth
/// at all here either) and the rest of this session's "administrative/v1" routes — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for the underlying prerender-crash
/// reason. Worth flagging separately from the others though: this one leaks live query text and
/// connection info, which is more sensitive than a client list. Tighten this (e.g. restore
/// [Authorize(Roles = Administrator)], matching <see cref="DatabaseController"/>) once the
/// frontend's prerender-safe-anonymous-call pattern is fixed and routes can require auth again.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/Database")]
public class AdministrativeDatabaseController(TraccarDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DatabaseStats>> GetStats() => await DatabaseStatsService.GetStatsAsync(db);
}
