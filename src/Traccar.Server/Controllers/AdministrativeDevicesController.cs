using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "Devices" routes, at the exact paths the Blazor
/// fleet-management frontend's AdminDevicesService calls — distinct from
/// <see cref="AdminDevicesController"/>'s "api/admin-devices" convention, which nothing in this
/// frontend actually calls.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/Devices/device")]
public class AdministrativeDevicesController(TraccarDbContext db) : ControllerBase
{
    private static AdminDevice ToAdminDevice(Device d) => new()
    {
        Id = d.Id,
        UniqueId = d.UniqueId,
        Model = d.Model,
        GsmNumber = d.Phone,
        IsActive = !d.Disabled,
        ClientId = d.ClientId,
        SerialNo = d.SerialNo,
    };

    [HttpGet("{clientId:int}")]
    public async Task<ActionResult<List<AdminDevice>>> Get(int clientId) =>
        (await db.Devices.Where(d => d.ClientId == clientId).OrderBy(d => d.Id).ToListAsync())
            .Select(ToAdminDevice).ToList();

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
            Model = device.Model,
            Phone = device.GsmNumber,
            SerialNo = device.SerialNo,
            Disabled = !device.IsActive,
        };
        db.Devices.Add(newDevice);
        await db.SaveChangesAsync();
        return ToAdminDevice(newDevice);
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
        existing.Model = device.Model ?? existing.Model;
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
        if (device != null)
        {
            db.Devices.Remove(device);
            await db.SaveChangesAsync();
        }
        return Ok();
    }
}

/// <summary>
/// Port of MockApi's device-model catalog and cross-client "move" routes — standalone (not
/// clients/device-scoped), so kept out of <see cref="AdministrativeDevicesController"/> proper.
/// </summary>
[ApiController]
[AllowAnonymous]
public class AdministrativeDeviceUtilitiesController(TraccarDbContext db) : ControllerBase
{
    [HttpGet("administrative/api/uploads/models")]
    public ActionResult<IReadOnlyList<DeviceModelInfo>> GetModels() => Ok(DeviceModelInfo.Catalog);

    [HttpPost("administrative/v1/api/move/assets")]
    public async Task<IActionResult> Move(AdministrativeMoveRequest request)
    {
        var identifiers = request.ToMove.Where(m => m.Checked)
            .Select(m => m.Identifier.ToString())
            .ToHashSet();

        var client = await db.Clients.FindAsync(request.ToClientId);
        if (client != null)
        {
            var currentCount = await db.Devices.CountAsync(
                d => d.ClientId == request.ToClientId && !identifiers.Contains(d.UniqueId));
            if (currentCount + identifiers.Count > client.DeviceLimit)
            {
                return Conflict($"\"{client.Name}\" has reached its device limit ({client.DeviceLimit}) — cannot move {identifiers.Count} device(s) onto it.");
            }
        }

        await db.Devices
            .Where(d => identifiers.Contains(d.UniqueId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.ClientId, request.ToClientId));
        return Ok();
    }
}
