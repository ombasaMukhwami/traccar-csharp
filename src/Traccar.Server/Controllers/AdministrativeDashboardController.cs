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

    /// <summary>Port of MockApi's "Dashboard/recent-alerts" route. There is no separate
    /// user-defined "Alert" backend yet, so this surfaces the real <see cref="Event.TypeAlarm"/>
    /// rows the protocol pipeline already records — the same data the Reports/Events pages use —
    /// rather than a synthetic feed.</summary>
    [HttpGet("recent-alerts")]
    public async Task<ActionResult<List<DashboardAlertNotification>>> GetRecentAlerts(
        [FromQuery] int? resellerId, [FromQuery] int take = 8)
    {
        var deviceIds = await (resellerId is > 0
                ? db.Devices.Where(d => db.Clients.Any(c => c.Id == d.ClientId && c.ParentId == resellerId))
                : db.Devices)
            .Select(d => d.Id)
            .ToListAsync();

        var events = await db.Events
            .Where(e => e.Type == Event.TypeAlarm && deviceIds.Contains(e.DeviceId))
            .OrderByDescending(e => e.EventTime)
            .Take(take)
            .ToListAsync();

        var deviceNames = await db.Devices
            .Where(d => deviceIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name);

        var positionIds = events.Select(e => e.PositionId).Distinct().ToList();
        var positions = await db.Positions
            .Where(p => positionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Address);

        return events.Select(e =>
        {
            var alarm = e.GetString(Position.KeyAlarm);
            deviceNames.TryGetValue(e.DeviceId, out var deviceName);
            positions.TryGetValue(e.PositionId, out var address);
            return new DashboardAlertNotification
            {
                AlertName = alarm is null ? "Alert" : HumanizeAlarmName(alarm),
                AssetName = deviceName,
                AlertTime = e.EventTime ?? DateTime.UtcNow,
                LocationText = address,
            };
        }).ToList();
    }

    private static string HumanizeAlarmName(string alarm)
    {
        var spaced = System.Text.RegularExpressions.Regex.Replace(alarm, "(?<!^)([A-Z])", " $1");
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
