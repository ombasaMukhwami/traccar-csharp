using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Model.Reports;
using Traccar.Server.Reports;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's TelemetryEndpoints "Trips" route — unlike MockApi's synthetic
/// <c>TripGenerator</c>, this runs the real trip-detection algorithm
/// (<see cref="ReportUtils.DetectTripsAndStopsAsync{T}"/>, the same one backing the Trips report)
/// against persisted <see cref="Position"/> rows, then replays each detected leg's own positions
/// as playback frames. The frontend's <see cref="Trip"/> DTO expects pre-segmented legs, not a
/// flat position list — an earlier version of this endpoint returned the whole window as one
/// unsegmented list, which crashed TripSearchPanel.razor's per-trip First()/Last() calls.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("telemetry/v1/api/Telemetrics")]
public class TelemetricsController(TraccarDbContext db, ReportUtils reportUtils) : ControllerBase
{
    private const double KnotsToKph = 1.852;

    [HttpPost("trips")]
    public async Task<ActionResult<List<Trip>>> GetTrips(TripSearchRequest request)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.UniqueId == request.Identifier.ToString());
        if (device == null)
        {
            return new List<Trip>();
        }

        // Npgsql only accepts DateTimeKind.Utc for a "timestamp with time zone" column — the
        // frontend's JSON can arrive as Utc, Local (an offset that happened to match the
        // server's own timezone), or Unspecified (no offset at all), so each needs its own
        // conversion rather than a blind SpecifyKind, which would silently shift a genuine
        // Local instant by reinterpreting its clock digits as UTC.
        var start = AsUtc(request.StartDate);
        var end = AsUtc(request.EndDate);

        var tripItems = await reportUtils.DetectTripsAndStopsAsync<TripReportItem>(device, start, end);

        var result = new List<Trip>();
        foreach (var item in tripItems)
        {
            var positions = await db.Positions
                .Where(p => p.DeviceId == device.Id && p.FixTime >= item.StartTime && p.FixTime <= item.EndTime)
                .OrderBy(p => p.FixTime)
                .ToListAsync();

            // A detected leg with no positions in its own [StartTime, EndTime] window shouldn't
            // be possible (the window comes from that leg's own start/end position timestamps),
            // but skip rather than emit an empty TripViewModel — the frontend calls
            // First()/Last() on it unconditionally.
            if (positions.Count == 0)
            {
                continue;
            }

            result.Add(new Trip
            {
                TripViewModel = positions
                    .Select(p => new TripFrame(
                        p.Latitude, p.Longitude, p.DeviceTime ?? p.FixTime ?? p.ServerTime, p.Address, p.Speed, p.Course))
                    .ToList(),
                DistanceCovered = Math.Round(item.Distance, 2),
                AverageSpeed = Math.Round(item.AverageSpeed * KnotsToKph, 1),
                TotalSeconds = item.Duration / 1000.0,
                TripDuration = FormatDuration(item.Duration),
            });
        }
        return result;
    }

    private static string FormatDuration(long durationMs)
    {
        var totalSeconds = (int)(durationMs / 1000);
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds - hours * 3600) / 60;
        var seconds = totalSeconds - hours * 3600 - minutes * 60;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    private static DateTime AsUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
    };
}
