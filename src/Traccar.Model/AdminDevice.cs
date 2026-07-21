namespace Traccar.Model;

/// <summary>
/// Simplified device-admin-screen projection of <see cref="Device"/> — the admin device list/edit
/// UI doesn't need every fleet field, just the device-facing ones plus its currently assigned
/// asset(s). Not a persisted entity of its own; a controller projects it from Device.
/// </summary>
public class AdminDevice
{
    public long Id { get; set; }

    /// <summary>Maps to <see cref="Device.UniqueId"/> — the protocol-facing device identifier
    /// (IMEI etc.), not a separate identifier of its own.</summary>
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>Maps to <see cref="Device.Model"/> (tracker hardware model).</summary>
    public string? DeviceModel { get; set; }

    /// <summary>Maps to <see cref="Device.Phone"/>.</summary>
    public string? GsmNumber { get; set; }

    /// <summary>Inverse of <see cref="Device.Disabled"/>.</summary>
    public bool IsActive { get; set; }

    public int ClientId { get; set; }

    public string? SerialNo { get; set; }

    public List<AssignedAsset> AssetsViewModels { get; set; } = [];

    public string AssignedAssetName => AssetsViewModels.FirstOrDefault(a => !a.IsDisAssociated)?.AssetName ?? "-";
}

public class AssignedAsset
{
    public string? AssetName { get; set; }

    public string? Tag { get; set; }

    public bool IsDisAssociated { get; set; }
}
