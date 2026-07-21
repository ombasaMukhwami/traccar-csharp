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
}