namespace Traccar.Model;

/// <summary>
/// Port of the Blazor frontend's ClientInclusive DTO — a client with its devices (each carrying
/// its single "asset", matching how this simplified model folds Device+Asset into one row) for
/// AlertEditorDialog.razor's asset picker.
/// </summary>
public class ClientInclusive
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public List<DeviceInclusive> Devices { get; set; } = [];
}

public class DeviceInclusive
{
    /// <summary>Maps to <see cref="Device.UniqueId"/>.</summary>
    public string Identifier { get; set; } = string.Empty;

    public List<AssetInclusive> Asset { get; set; } = [];
}

public class AssetInclusive
{
    /// <summary>Reuses <see cref="Device.Id"/> — this simplified model has no separate asset
    /// entity, so device and asset are the same row.</summary>
    public int AssetId { get; set; }

    /// <summary>Maps to <see cref="Device.Name"/>.</summary>
    public string? Name { get; set; }

    public string? PlateNumber { get; set; }

    /// <summary>Always false — the frontend's separate "unassign an asset from its device"
    /// concept has no equivalent here (every Device already is its own asset).</summary>
    public bool IsDisAssociated { get; set; }
}
