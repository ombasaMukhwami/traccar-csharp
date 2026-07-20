using Traccar.Model;

namespace Traccar.Protocols.Handlers;

/// <summary>
/// Promotes selected position attributes to first-class typed properties so they are persisted
/// as dedicated columns rather than buried inside the JSON attributes blob.
/// Runs after all enrichment (including computed attributes) and only for positions that pass
/// FilterHandler, so it always sees the final, fully-enriched attribute values.
/// </summary>
public sealed class PositionAttributesHandler
{
    public void Extract(Position position)
    {
        position.Ignition = position.GetBoolean(Position.KeyIgnition);

        // KEY_ALARM may carry multiple comma-separated alarms; use the first for EventId.
        var alarmString = position.GetString(Position.KeyAlarm);
        if (alarmString != null)
        {
            var firstAlarm = alarmString.Contains(',')
                ? alarmString[..alarmString.IndexOf(',')]
                : alarmString;
            position.EventId = EventType.FromName(firstAlarm)?.Id ?? 0;
        }

        // Prefer device-reported odometer; fall back to server-accumulated total distance.
        position.Odometer = position.HasAttribute(Position.KeyOdometer)
            ? position.GetDouble(Position.KeyOdometer)
            : position.GetDouble(Position.KeyTotalDistance);
    }
}
