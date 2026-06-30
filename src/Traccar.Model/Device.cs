namespace Traccar.Model;

public class Device : GroupedModel
{
    public const string StatusUnknown = "unknown";
    public const string StatusOnline = "online";
    public const string StatusOffline = "offline";

    public long CalendarId { get; set; }

    public string? Name { get; set; }

    private string uniqueId = string.Empty;

    public string UniqueId
    {
        get => uniqueId;
        set => uniqueId = value.Trim();
    }

    private string status = StatusOffline;

    public string Status
    {
        get => status;
        set => status = value?.Trim() ?? StatusOffline;
    }

    public DateTime? LastUpdate { get; set; }

    public long PositionId { get; set; }

    public string? Phone { get; set; }

    public string? Model { get; set; }

    public string? Contact { get; set; }

    public string? Category { get; set; }

    public bool Disabled { get; set; }

    public DateTime? ExpirationTime { get; set; }
}
