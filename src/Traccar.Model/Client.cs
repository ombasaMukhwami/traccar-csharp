namespace Traccar.Model;

/// <summary>
/// A reseller/client tenant. A "reseller" is a row with <see cref="ParentId"/> null (a root); a
/// "client" is a row whose ParentId points at another row (its reseller). Branding fields only
/// ever get populated on roots in practice, but live on every row since there's only one table.
/// </summary>
public class Client
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>Null means this row is a root (a reseller).</summary>
    public int? ParentId { get; set; }

    public string? PrimaryColor { get; set; }

    public string? SecondaryColor { get; set; }

    public string MapProvider { get; set; } = "OPEN_STREET";

    public string? MapboxAccessToken { get; set; }

    public string? GoogleMapsApiKey { get; set; }

    /// <summary>Max devices allowed on this client — enforced when creating a device or
    /// reassigning devices onto it.</summary>
    public int DeviceLimit { get; set; } = 110;

    /// <summary>IANA/system time zone id (e.g. "Africa/Nairobi"). Nullable to match the Blazor
    /// fleet-management frontend's own Client DTO, which sends null when unset — a non-nullable
    /// string here trips ASP.NET Core's implicit-required model validation on every edit that
    /// doesn't touch this field (i.e. almost all of them).</summary>
    public string? TimeZone { get; set; } = "Africa/Nairobi";

    /// <summary>Default coordinates for this client's devices — used as a new device's starting
    /// position when it hasn't reported one yet.</summary>
    public double? DefaultLatitude { get; set; }

    public double? DefaultLongitude { get; set; }
}
