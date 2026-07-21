using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "Devices" routes — the simplified admin-screen view
/// of <see cref="Device"/> (see <see cref="AdminDevice"/>), enforcing each client's device limit.
/// Distinct from <see cref="DevicesController"/>, which manages the full Device entity used by the
/// protocol-decoding pipeline.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin-devices")]
public class AdminDevicesController(TraccarDbContext db) : ControllerBase
{
    private static AdminDevice ToAdminDevice(Device d) => new()
    {
        Id = d.Id,
        UniqueId = d.UniqueId,
        DeviceModel = d.Model,
        GsmNumber = d.Phone,
        IsActive = !d.Disabled,
        ClientId = d.ClientId,
        SerialNo = d.SerialNo,
    };

    [HttpGet]
    public async Task<ActionResult<List<AdminDevice>>> Get([FromQuery] int clientId)
    {
        var devices = await db.Devices.Where(d => d.ClientId == clientId).OrderBy(d => d.Id).ToListAsync();
        return devices.Select(ToAdminDevice).ToList();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AdminDevice>> GetById(long id)
    {
        var device = await db.Devices.FindAsync(id);
        return device == null ? NotFound() : ToAdminDevice(device);
    }

    [HttpGet("models")]
    public ActionResult<IReadOnlyList<DeviceModelInfo>> GetModels() => Ok(DeviceModelInfo.Catalog);

    [HttpPost]
    public async Task<ActionResult<AdminDevice>> Create(AdminDevice device)
    {
        var client = await db.Clients.FindAsync(device.ClientId);
        if (client != null)
        {
            var currentCount = await db.Devices.CountAsync(d => d.ClientId == device.ClientId);
            if (currentCount >= client.DeviceLimit)
            {
                return Conflict($"\"{client.Name}\" has reached its device limit ({client.DeviceLimit}).");
            }
        }

        var newDevice = new Device
        {
            UniqueId = device.UniqueId,
            ClientId = device.ClientId,
            Model = device.DeviceModel,
            Phone = device.GsmNumber,
            SerialNo = device.SerialNo,
            Disabled = !device.IsActive,
        };
        db.Devices.Add(newDevice);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = newDevice.Id }, ToAdminDevice(newDevice));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AdminDevice>> Update(long id, AdminDevice device)
    {
        var existing = await db.Devices.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.UniqueId = device.UniqueId;
        existing.Model = device.DeviceModel ?? existing.Model;
        existing.Phone = device.GsmNumber ?? existing.Phone;
        existing.SerialNo = device.SerialNo ?? existing.SerialNo;
        existing.Disabled = !device.IsActive;
        await db.SaveChangesAsync();
        return ToAdminDevice(existing);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var device = await db.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }
        db.Devices.Remove(device);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("move")]
    public async Task<IActionResult> Move(MoveDevicesRequest request)
    {
        var uniqueIds = request.UniqueIds.ToHashSet();

        var client = await db.Clients.FindAsync(request.ToClientId);
        if (client != null)
        {
            // Devices already on the destination client that are ALSO in this move (re-moving
            // the same device to where it already is) shouldn't be double-counted.
            var currentCount = await db.Devices.CountAsync(d => d.ClientId == request.ToClientId && !uniqueIds.Contains(d.UniqueId));
            if (currentCount + uniqueIds.Count > client.DeviceLimit)
            {
                return Conflict($"\"{client.Name}\" has reached its device limit ({client.DeviceLimit}) — cannot move {uniqueIds.Count} device(s) onto it.");
            }
        }

        await db.Devices
            .Where(d => uniqueIds.Contains(d.UniqueId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.ClientId, request.ToClientId));
        return NoContent();
    }
}
