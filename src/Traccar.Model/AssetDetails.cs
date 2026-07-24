namespace Traccar.Model;

/// <summary>
/// Port of the Blazor frontend's AssetDetails DTO — the asset-management view/edit shape,
/// projected from (and applied back onto) <see cref="Device"/>'s asset fields.
/// </summary>
public class AssetDetails
{
    /// <summary>Same as <see cref="DeviceId"/> in this simplified model — see
    /// <see cref="Device"/>'s own doc comment on its asset fields.</summary>
    public int Id { get; set; }

    public int DeviceId { get; set; }

    public string AssetName { get; set; } = string.Empty;

    /// <summary>Maps to <see cref="Device.Name"/> — same value as <see cref="AssetName"/>, this
    /// simplified model has no separate tag field.</summary>
    public string? Tag { get; set; }

    public int TrackingObject { get; set; } = 1;

    public string? Contact { get; set; }

    /// <summary>Vehicle/asset model. Maps to <see cref="Device.VehicleModel"/>.</summary>
    public string? Model { get; set; }

    public string? Make { get; set; }

    public string? Color { get; set; }

    public string? ChasisNo { get; set; }

    /// <summary>Not persisted — always synthesized as "1 year ago"/"1 year from now" on read,
    /// matching MockApi's own behavior (it never actually stores these either).</summary>
    public List<string> FittingDate { get; set; } = [];

    public List<string> ExpiryDate { get; set; } = [];

    /// <summary>4 = "toggle associate/disassociate" on Update; anything else applies the other
    /// fields normally.</summary>
    public int? Mode { get; set; }

    public string? OwnerName { get; set; }

    public string? OwnerId { get; set; }

    /// <summary>References <see cref="AgentDetails.Id"/>.</summary>
    public int? Agent { get; set; }

    public string? AssetCertificateNo { get; set; }

    public bool IsDisAssociated { get; set; }
}

/// <summary>Port of the Blazor frontend's fixed TrackingObjects catalog.</summary>
public static class TrackingObjects
{
    public static readonly (int Id, string Name)[] All =
    [
        (1, "Car"), (2, "Bus"), (3, "Lorry"), (4, "Motorcycle"),
        (5, "Bicycle"), (6, "Pet"), (7, "Animal"), (20, "Other"),
    ];
}
