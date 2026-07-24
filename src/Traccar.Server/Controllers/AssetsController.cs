using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "Assets" routes. One Device per asset — models the
/// device's *current* assignment rather than a true multi-asset history, matching how this
/// simplified schema folds Device+Asset into a single row.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/Assets/asset")]
public class AssetsController(TraccarDbContext db) : ControllerBase
{
    // MockApi hardcodes these to "1 year ago"/"1 year from now" on every read rather than
    // persisting them — this port keeps that exact behavior instead of inventing real storage
    // for fields the reference app never actually saves.
    private static AssetDetails ToAssetDetails(Device d) => new()
    {
        Id = (int)d.Id,
        DeviceId = (int)d.Id,
        AssetName = d.Name ?? string.Empty,
        Tag = d.Name,
        TrackingObject = d.TrackingObject,
        Contact = d.OwnerContact,
        Model = d.VehicleModel,
        Make = d.Make,
        Color = d.Color,
        ChasisNo = d.ChasisNo,
        FittingDate = [DateTime.UtcNow.AddYears(-1).ToString("s")],
        ExpiryDate = [DateTime.UtcNow.AddYears(1).ToString("s")],
        OwnerName = d.OwnerName,
        OwnerId = d.OwnerId,
        Agent = d.Agent,
        AssetCertificateNo = d.AssetCertificateNo,
        IsDisAssociated = d.IsDisAssociated,
    };

    private static void ApplyAssetFields(Device device, AssetDetails asset)
    {
        device.Name = asset.AssetName;
        device.TrackingObject = asset.TrackingObject;
        device.OwnerContact = asset.Contact ?? device.OwnerContact;
        device.VehicleModel = asset.Model ?? device.VehicleModel;
        device.Make = asset.Make ?? device.Make;
        device.Color = asset.Color ?? device.Color;
        device.ChasisNo = asset.ChasisNo ?? device.ChasisNo;
        device.OwnerName = asset.OwnerName ?? device.OwnerName;
        device.OwnerId = asset.OwnerId ?? device.OwnerId;
        device.Agent = asset.Agent;
        device.AssetCertificateNo = asset.AssetCertificateNo ?? device.AssetCertificateNo;
    }

    [HttpGet("{deviceId:long}")]
    public async Task<ActionResult<List<AssetDetails>>> Get(long deviceId)
    {
        var device = await db.Devices.FindAsync(deviceId);
        return device != null && !string.IsNullOrEmpty(device.Name)
            ? new List<AssetDetails> { ToAssetDetails(device) }
            : [];
    }

    [HttpPost]
    public async Task<ActionResult<AssetDetails>> Create(AssetDetails asset)
    {
        var device = await db.Devices.FindAsync((long)asset.DeviceId);
        if (device == null)
        {
            return NotFound();
        }
        ApplyAssetFields(device, asset);
        device.IsDisAssociated = false;
        await db.SaveChangesAsync();
        return ToAssetDetails(device);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AssetDetails>> Update(long id, AssetDetails asset)
    {
        var device = await db.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        if (asset.Mode == 4)
        {
            // activateAssetHelper()'s "associate/disassociate" toggle.
            device.IsDisAssociated = !device.IsDisAssociated;
        }
        else
        {
            ApplyAssetFields(device, asset);
        }

        await db.SaveChangesAsync();
        return ToAssetDetails(device);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var device = await db.Devices.FindAsync(id);
        if (device != null)
        {
            device.Name = "";
            device.IsDisAssociated = true;
            await db.SaveChangesAsync();
        }
        return Ok();
    }
}
