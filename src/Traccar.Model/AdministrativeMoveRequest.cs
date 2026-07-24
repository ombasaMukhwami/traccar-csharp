namespace Traccar.Model;

/// <summary>
/// Port of the Blazor frontend's MoveDevicesRequest/MoveDeviceItem DTOs, used by
/// "administrative/v1/api/move/assets" — a different shape from the existing
/// <see cref="MoveDevicesRequest"/> (which takes a flat UniqueIds list), so this is a distinct
/// type rather than a rename.
/// </summary>
public class AdministrativeMoveRequest
{
    public int FromClientId { get; set; }

    public int ToClientId { get; set; }

    public List<AdministrativeMoveItem> ToMove { get; set; } = [];
}

public class AdministrativeMoveItem
{
    /// <summary>Maps to <see cref="Device.UniqueId"/> (sent as a number by the frontend).</summary>
    public long Identifier { get; set; }

    public bool Checked { get; set; } = true;

    public string? AssetName { get; set; }
}
