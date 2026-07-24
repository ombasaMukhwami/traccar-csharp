namespace Traccar.Model;

public class Device : ExtendedModel
{
    public const string StatusUnknown = "unknown";
    public const string StatusOnline = "online";
    public const string StatusOffline = "offline";
    /// <summary>Display name. Also serves as the "AssetName" field from the fleet-management side.</summary>
    public string? Name { get; set; }

    private string _uniqueId = string.Empty;

    public string UniqueId
    {
        get => _uniqueId;
        set => _uniqueId = value.Trim();
    }

    private string _status = StatusOffline;

    public string Status
    {
        get => _status;
        set => _status = value?.Trim() ?? StatusOffline;
    }

    public DateTime? LastUpdate { get; set; }

    public long? PositionId { get; set; }

    /// <summary>Also serves as the "GsmNumber" field from the fleet-management side.</summary>
    public string? Phone { get; set; }

    /// <summary>GPS tracker hardware model (e.g. "GT06", "NT20") — protocol decoders branch on
    /// this via GetDeviceModel(). Not to be confused with <see cref="VehicleModel"/>.</summary>
    public string? Model { get; set; }
  
    /// <summary>Also serves as the inverse of the "IsActive" field from the fleet-management side.</summary>
    public bool Disabled { get; set; }

    public DateTime? ExpirationTime { get; set; }
    public int ClientId { get; set; }
    public string? SerialNo { get; set; }

    // --- Asset fields (administrative/v1/api/Assets/asset) ---
    // This model folds the fleet-management frontend's separate Device+Asset entities into one
    // row (Name already doubles as AssetName above), so these live directly on Device rather
    // than a distinct Asset table.

    /// <summary>One of the frontend's fixed TrackingObjects catalog (1=Car, 2=Bus, ...).</summary>
    public int TrackingObject { get; set; } = 1;

    public string? OwnerContact { get; set; }

    /// <summary>Vehicle/asset model — distinct from <see cref="Model"/>, which is the tracker
    /// hardware model.</summary>
    public string? VehicleModel { get; set; }

    public string? Make { get; set; }
    public string? Color { get; set; }
    public string? ChasisNo { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerId { get; set; }

    /// <summary>References <see cref="AgentDetails.Id"/>.</summary>
    public int? Agent { get; set; }

    public string? AssetCertificateNo { get; set; }

    /// <summary>True once the asset has been explicitly unassigned from this device (see
    /// AssetsController's Delete) — a disassociated device keeps its Name cleared but the row
    /// itself isn't removed.</summary>
    public bool IsDisAssociated { get; set; }
}