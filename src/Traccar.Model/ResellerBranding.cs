namespace Traccar.Model;

/// <summary>
/// Just enough of a reseller <see cref="Client"/> row to theme a login page before anyone signs
/// in — not the full Client record, which also carries contact details that don't belong on a
/// pre-auth response. Projected from Client, not a persisted entity of its own.
/// </summary>
public class ResellerBranding
{
    public string Name { get; set; } = string.Empty;

    public string? PrimaryColor { get; set; }

    public string? SecondaryColor { get; set; }

    public string MapProvider { get; set; } = "OPEN_STREET";

    public string? MapboxAccessToken { get; set; }

    public string? GoogleMapsApiKey { get; set; }
}
