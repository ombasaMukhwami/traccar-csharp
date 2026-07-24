namespace Traccar.Model;

/// <summary>An asset's last known position, as embedded in <see cref="LiveAsset"/> — port of the
/// Blazor frontend's LivePosition DTO. Projected from <see cref="Position"/>, not persisted.</summary>
public class LivePosition
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Maps to <see cref="Position.Course"/>.</summary>
    public double Heading { get; set; }

    public double Speed { get; set; }

    public double Odometer { get; set; }

    public double Battery { get; set; }

    /// <summary>Maps to <see cref="Position.Ignition"/>.</summary>
    public bool IgnitionState { get; set; }

    public int EventId { get; set; }

    public string? EventName { get; set; }

    public DateTime? GpsDateTime { get; set; }

    /// <summary>Maps to <see cref="Position.Address"/>.</summary>
    public string? LocationText { get; set; }

    public double? Altitude { get; set; }
}
