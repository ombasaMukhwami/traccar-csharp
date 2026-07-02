using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Traccar.Model.Reports;
using Traccar.Server.Reports;

namespace Traccar.Server.Controllers;

/// <summary>
/// Mirrors Java's ReportResource.  Excel (JXLS) export is not implemented — JSON only.
/// All endpoints require an authenticated session.
/// </summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(
    TripsReportProvider tripsProvider,
    StopsReportProvider stopsProvider,
    SummaryReportProvider summaryProvider,
    DevicesReportProvider devicesProvider) : ControllerBase
{
    private long CurrentUserId =>
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // -------------------------------------------------------------------------
    // Trips
    // -------------------------------------------------------------------------

    [HttpGet("trips")]
    public async Task<ActionResult<List<TripReportItem>>> GetTrips(
        [FromQuery(Name = "deviceId")] List<long>? deviceIds,
        [FromQuery(Name = "groupId")] List<long>? groupIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (from is null || to is null)
            return BadRequest("from and to are required");

        try
        {
            return await tripsProvider.GetObjectsAsync(
                CurrentUserId, deviceIds ?? [], groupIds ?? [], from.Value, to.Value);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Stops
    // -------------------------------------------------------------------------

    [HttpGet("stops")]
    public async Task<ActionResult<List<StopReportItem>>> GetStops(
        [FromQuery(Name = "deviceId")] List<long>? deviceIds,
        [FromQuery(Name = "groupId")] List<long>? groupIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (from is null || to is null)
            return BadRequest("from and to are required");

        try
        {
            return await stopsProvider.GetObjectsAsync(
                CurrentUserId, deviceIds ?? [], groupIds ?? [], from.Value, to.Value);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Summary
    // -------------------------------------------------------------------------

    [HttpGet("summary")]
    public async Task<ActionResult<List<SummaryReportItem>>> GetSummary(
        [FromQuery(Name = "deviceId")] List<long>? deviceIds,
        [FromQuery(Name = "groupId")] List<long>? groupIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool daily = false)
    {
        if (from is null || to is null)
            return BadRequest("from and to are required");

        try
        {
            return await summaryProvider.GetObjectsAsync(
                CurrentUserId, deviceIds ?? [], groupIds ?? [], from.Value, to.Value, daily);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Devices (fleet snapshot — no date range)
    // -------------------------------------------------------------------------

    [HttpGet("devices")]
    public async Task<ActionResult<List<DeviceReportItem>>> GetDevices() =>
        await devicesProvider.GetObjectsAsync(CurrentUserId);
}
