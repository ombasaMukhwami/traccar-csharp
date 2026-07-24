using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Server.Hubs;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "LiveClients" routes — backs the live-map sidebar's
/// client accordion (<see cref="LiveClient"/> list) and, per client, its asset rows
/// (<see cref="LiveAsset"/>, each carrying its device's last known <see cref="Position"/> via
/// <see cref="LiveTelemetryMapper"/>). Unlike MockApi's in-memory-only simulator, this reads real
/// persisted positions, so it reflects whatever the protocol pipeline last saved.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/LiveClients/Clients")]
public class LiveClientsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LiveClient>>> Get([FromQuery] int? resellerId)
    {
        var query = resellerId is > 0
            ? db.Clients.Where(c => c.ParentId == resellerId)
            : db.Clients.Where(c => c.ParentId != null);
        var clients = await query.OrderBy(c => c.Id).ToListAsync();
        return clients.Select(c => new LiveClient { Id = c.Id, Name = c.Name, IsDefault = c.IsDefault }).ToList();
    }

    [HttpGet("{clientId:int}")]
    public async Task<ActionResult<List<LiveAsset>>> GetAssets(int clientId)
    {
        var devices = await db.Devices.Where(d => d.ClientId == clientId).OrderBy(d => d.Id).ToListAsync();

        var positionIds = devices.Where(d => d.PositionId is > 0).Select(d => d.PositionId!.Value).ToList();
        var positionsById = positionIds.Count == 0
            ? []
            : await db.Positions.Where(p => positionIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        return devices.Select(d => new LiveAsset
        {
            Identifier = d.UniqueId,
            ClientId = d.ClientId,
            GsmNumber = d.Phone,
            DeviceModel = d.Model,
            AssetDisplay = new AssetDisplay { Name = d.Name, Tag = d.Name, OwnerContact = d.OwnerContact },
            LastPosition = d.PositionId is > 0 && positionsById.TryGetValue(d.PositionId.Value, out var position)
                ? LiveTelemetryMapper.ToLivePosition(position)
                : null,
        }).ToList();
    }
}
