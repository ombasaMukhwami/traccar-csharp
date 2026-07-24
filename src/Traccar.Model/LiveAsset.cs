namespace Traccar.Model;

/// <summary>One row in the fleet-management live-map sidebar — port of the Blazor frontend's
/// LiveAsset DTO, projected from <see cref="Device"/> (+ its last <see cref="Position"/>).</summary>
public class LiveAsset
{
    /// <summary>Maps to <see cref="Device.UniqueId"/>.</summary>
    public string Identifier { get; set; } = string.Empty;

    public int ClientId { get; set; }

    /// <summary>Maps to <see cref="Device.Phone"/>.</summary>
    public string? GsmNumber { get; set; }

    /// <summary>Maps to <see cref="Device.Model"/>.</summary>
    public string? DeviceModel { get; set; }

    public AssetDisplay? AssetDisplay { get; set; }

    public LivePosition? LastPosition { get; set; }
}

/// <summary>Port of the Blazor frontend's AssetDisplay DTO.</summary>
public class AssetDisplay
{
    /// <summary>Maps to <see cref="Device.Name"/>.</summary>
    public string? Name { get; set; }

    /// <summary>Maps to <see cref="Device.Name"/> — this simplified model has no separate tag
    /// field, so Name doubles as both.</summary>
    public string? Tag { get; set; }

    /// <summary>Maps to <see cref="Device.OwnerContact"/>.</summary>
    public string? OwnerContact { get; set; }
}
