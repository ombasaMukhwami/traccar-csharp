using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

[ApiController]
[Route("api/positions")]
public class PositionsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Position>>> Get(
        [FromQuery] long deviceId = 0, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (deviceId > 0)
        {
            if (from.HasValue && to.HasValue)
            {
                return await db.Positions
                    .Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to)
                    .OrderBy(p => p.FixTime)
                    .ToListAsync();
            }

            var positionId = await db.Devices
                .Where(d => d.Id == deviceId)
                .Select(d => d.PositionId)
                .FirstOrDefaultAsync();

            return await db.Positions.Where(p => p.Id == positionId).ToListAsync();
        }

        var latestPositionIds = await db.Devices
            .Where(d => d.PositionId != 0)
            .Select(d => d.PositionId)
            .ToListAsync();

        return await db.Positions.Where(p => latestPositionIds.Contains(p.Id)).ToListAsync();
    }
}
