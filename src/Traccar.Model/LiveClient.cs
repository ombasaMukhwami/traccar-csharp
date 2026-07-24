namespace Traccar.Model;

/// <summary>Trimmed client projection for the fleet-management live-map sidebar's accordion
/// panels — port of the Blazor frontend's LiveClient DTO.</summary>
public class LiveClient
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
