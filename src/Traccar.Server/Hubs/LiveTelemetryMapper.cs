using Traccar.Model;

namespace Traccar.Server.Hubs;

/// <summary>
/// Projects <see cref="Position"/>/<see cref="Device"/> into the flat DTOs the Blazor
/// fleet-management frontend expects (<see cref="LivePosition"/> for the live-assets REST list,
/// <see cref="LivePositionUpdate"/> for the SignalR feed) — shared by
/// <see cref="Controllers.LiveClientsController"/> and <see cref="TelemetryHubPositionForwarder"/>
/// so both surfaces agree on field semantics.
/// </summary>
public static class LiveTelemetryMapper
{
    public static LivePosition ToLivePosition(Position position) => new()
    {
        Latitude = position.Latitude,
        Longitude = position.Longitude,
        Heading = position.Course,
        Speed = position.Speed,
        Odometer = position.Odometer,
        Battery = GetBattery(position),
        IgnitionState = position.Ignition,
        EventId = position.EventId,
        EventName = EventType.FromId(position.EventId)?.Name,
        GpsDateTime = position.DeviceTime ?? position.FixTime ?? position.ServerTime,
        LocationText = position.Address,
        Altitude = position.Altitude,
    };

    public static LivePositionUpdate ToLivePositionUpdate(Position position, Device? device) => new()
    {
        Identifier = device?.UniqueId ?? string.Empty,
        Latitude = position.Latitude,
        Longitude = position.Longitude,
        Speed = position.Speed,
        Heading = position.Course,
        IgnitionState = position.Ignition,
        EventId = position.EventId,
        EventName = EventType.FromId(position.EventId)?.Name,
        GpsDateTime = position.DeviceTime ?? position.FixTime ?? position.ServerTime,
        Odometer = position.Odometer,
        Battery = GetBattery(position),
        AssetName = device?.Name,
        LocationText = position.Address,
        Altitude = position.Altitude,
    };

    private static double GetBattery(Position position) =>
        position.HasAttribute(Position.KeyBatteryLevel)
            ? position.GetDouble(Position.KeyBatteryLevel)
            : position.GetDouble(Position.KeyBattery);
}
