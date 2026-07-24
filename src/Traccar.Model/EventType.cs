namespace Traccar.Model;

public sealed class EventType
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public static readonly IReadOnlyList<EventType> All =
    [
        new() { Id =  1, Name = Position.AlarmGeneral,        Description = "General-purpose alarm with no specific category." },
        new() { Id =  2, Name = Position.AlarmSos,            Description = "Emergency SOS alert triggered manually by the driver." },
        new() { Id =  3, Name = Position.AlarmVibration,      Description = "Vibration or shock detected on the device or vehicle." },
        new() { Id =  4, Name = Position.AlarmMovement,       Description = "Unexpected vehicle movement detected while ignition is off." },
        new() { Id =  5, Name = Position.AlarmLowSpeed,       Description = "Vehicle speed dropped below the configured minimum threshold." },
        new() { Id =  6, Name = Position.AlarmOverspeed,      Description = "Vehicle speed exceeded the configured maximum threshold." },
        new() { Id =  7, Name = Position.AlarmFallDown,       Description = "Free-fall or sudden impact detected; used primarily on wearable and asset trackers." },
        new() { Id =  8, Name = Position.AlarmLowPower,       Description = "External power supply voltage has fallen below the safe operating level." },
        new() { Id =  9, Name = Position.AlarmLowBattery,     Description = "Internal battery charge is critically low." },
        new() { Id = 10, Name = Position.AlarmFault,          Description = "General device or vehicle fault condition reported." },
        new() { Id = 11, Name = Position.AlarmPowerOff,       Description = "External power supply to the tracker was disconnected." },
        new() { Id = 12, Name = Position.AlarmPowerOn,        Description = "External power supply to the tracker was reconnected." },
        new() { Id = 13, Name = Position.AlarmDoor,           Description = "Door opened or closed unexpectedly." },
        new() { Id = 14, Name = Position.AlarmLock,           Description = "Vehicle or asset lock was activated." },
        new() { Id = 15, Name = Position.AlarmUnlock,         Description = "Vehicle or asset lock was deactivated." },
        new() { Id = 16, Name = Position.AlarmGeofence,       Description = "Geofence boundary was crossed (generic, direction unspecified)." },
        new() { Id = 17, Name = Position.AlarmGeofenceEnter,  Description = "Vehicle entered a defined geofence zone." },
        new() { Id = 18, Name = Position.AlarmGeofenceExit,   Description = "Vehicle exited a defined geofence zone." },
        new() { Id = 19, Name = Position.AlarmGpsAntennaCut,  Description = "GPS antenna cable has been cut or disconnected — possible tampering." },
        new() { Id = 20, Name = Position.AlarmAccident,       Description = "Collision or high-G impact detected." },
        new() { Id = 21, Name = Position.AlarmTow,            Description = "Vehicle is being towed while the ignition is off." },
        new() { Id = 22, Name = Position.AlarmIdle,           Description = "Engine has been running with no movement for longer than the configured idle limit." },
        new() { Id = 23, Name = Position.AlarmHighRpm,        Description = "Engine RPM exceeded the configured threshold." },
        new() { Id = 24, Name = Position.AlarmAcceleration,   Description = "Sudden hard-acceleration event detected." },
        new() { Id = 25, Name = Position.AlarmBraking,        Description = "Sudden hard-braking event detected." },
        new() { Id = 26, Name = Position.AlarmCornering,      Description = "Sharp cornering or hard-turning manoeuvre detected." },
        new() { Id = 27, Name = Position.AlarmLaneChange,     Description = "Abrupt lane-change manoeuvre detected." },
        new() { Id = 28, Name = Position.AlarmFatigueDriving, Description = "Driver has exceeded continuous driving time beyond the configured fatigue limit." },
        new() { Id = 29, Name = Position.AlarmPowerCut,       Description = "Main power supply to the tracker was severed — likely tampering." },
        new() { Id = 30, Name = Position.AlarmPowerRestored,  Description = "Main power supply to the tracker was restored after a cut." },
        new() { Id = 31, Name = Position.AlarmJamming,        Description = "GPS or GSM signal jamming detected in the vicinity." },
        new() { Id = 32, Name = Position.AlarmTemperature,    Description = "Monitored temperature exceeded the configured high or low threshold." },
        new() { Id = 33, Name = Position.AlarmParking,        Description = "Vehicle entered or exited a parking state." },
        new() { Id = 34, Name = Position.AlarmBonnet,         Description = "Vehicle bonnet (hood) was opened." },
        new() { Id = 35, Name = Position.AlarmFootBrake,      Description = "Foot brake or parking brake was applied." },
        new() { Id = 36, Name = Position.AlarmFuelLeak,       Description = "Fuel level drop inconsistent with normal consumption — possible leak or theft." },
        new() { Id = 37, Name = Position.AlarmTampering,      Description = "Physical tampering or device case opening detected." },
        new() { Id = 38, Name = Position.AlarmRemoving,       Description = "Device is being removed from the vehicle." },
    ];

    public static EventType? FromName(string name) =>
        All.FirstOrDefault(e => e.Name == name);

    public static EventType? FromId(int id) =>
        All.FirstOrDefault(e => e.Id == id);
}
