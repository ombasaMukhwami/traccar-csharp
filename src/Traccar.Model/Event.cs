namespace Traccar.Model;

public class Event : Message
{
    public const string TypeCommandResult = "commandResult";
    public const string TypeDeviceOnline = "deviceOnline";
    public const string TypeDeviceUnknown = "deviceUnknown";
    public const string TypeDeviceOffline = "deviceOffline";
    public const string TypeQueuedCommandSent = "queuedCommandSent";
    public const string TypeAlarm = "alarm";
    public const string TypeIgnitionOn = "ignitionOn";
    public const string TypeIgnitionOff = "ignitionOff";

    public Event() { }

    public Event(string type, Position position)
    {
        Type = type;
        PositionId = position.Id;
        DeviceId = position.DeviceId;
        EventTime = position.DeviceTime;
    }

    public Event(string type, long deviceId)
    {
        Type = type;
        DeviceId = deviceId;
        EventTime = DateTime.UtcNow;
    }

    public DateTime? EventTime { get; set; }

    public long PositionId { get; set; }

    public long GeofenceId { get; set; }

    public long MaintenanceId { get; set; }
}
