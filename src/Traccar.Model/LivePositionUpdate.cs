namespace Traccar.Model;

/// <summary>
/// Payload of the telemetry hub's "BroadcastMessage" event — port of the Blazor frontend's
/// LivePositionUpdate DTO. Sent as a JSON-serialized string (not a structured SignalR argument)
/// because that's what the frontend's <c>Hub.On&lt;string&gt;("BroadcastMessage", ...)</c>
/// subscription expects; property names are PascalCase to match the frontend's own
/// case-sensitive <c>JsonSerializer.Deserialize&lt;LivePositionUpdate&gt;(json)</c> call — do not
/// route this through ASP.NET's camelCase-configured JSON options.
/// </summary>
public class LivePositionUpdate
{
    /// <summary>Maps to <see cref="Device.UniqueId"/>.</summary>
    public string Identifier { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double Speed { get; set; }

    /// <summary>Maps to <see cref="Position.Course"/>.</summary>
    public double Heading { get; set; }

    /// <summary>Maps to <see cref="Position.Ignition"/>.</summary>
    public bool IgnitionState { get; set; }

    public int EventId { get; set; }

    public string? EventName { get; set; }

    public DateTime GpsDateTime { get; set; }

    public double Odometer { get; set; }

    public double Battery { get; set; }

    /// <summary>Maps to <see cref="Device.Name"/>.</summary>
    public string? AssetName { get; set; }

    /// <summary>Maps to <see cref="Position.Address"/>.</summary>
    public string? LocationText { get; set; }

    public double? Altitude { get; set; }
}
