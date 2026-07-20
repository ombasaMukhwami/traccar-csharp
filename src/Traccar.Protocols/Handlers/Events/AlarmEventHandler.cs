using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;

namespace Traccar.Protocols.Handlers.Events;

public sealed class AlarmEventHandler(
    IConfiguration configuration,
    ILogger<AlarmEventHandler> logger) : BaseEventHandler(logger)
{
    private readonly bool _ignoreDuplicates =
        configuration.GetValue(ConfigKeys.Events.IgnoreDuplicateAlerts, false);

    protected override ValueTask OnPositionAsync(Position position, Position? last, Action<Event> callback)
    {
        var alarmString = position.GetString(Position.KeyAlarm);
        if (alarmString == null)
            return ValueTask.CompletedTask;

        var alarms = new HashSet<string>(alarmString.Split(',', StringSplitOptions.RemoveEmptyEntries));

        if (_ignoreDuplicates && last != null)
        {
            var lastAlarmString = last.GetString(Position.KeyAlarm);
            if (lastAlarmString != null)
                alarms.ExceptWith(lastAlarmString.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var alarm in alarms)
        {
            var ev = new Event(Event.TypeAlarm, position);
            ev.Set(Position.KeyAlarm, alarm);
            callback(ev);
        }

        return ValueTask.CompletedTask;
    }
}
