using Traccar.Model;

namespace Traccar.Protocols.Handlers;

public sealed class EngineHoursHandler
{
    public void Process(Position position, Position? last)
    {
        if (position.HasAttribute(Position.KeyHours) || last == null) return;

        long hours = last.GetLong(Position.KeyHours);
        if (last.GetBoolean(Position.KeyIgnition) && position.GetBoolean(Position.KeyIgnition))
        {
            var lastTime = (DateTimeOffset)(last.DeviceTime ?? last.FixTime ?? last.ServerTime);
            var curTime = (DateTimeOffset)(position.DeviceTime ?? position.FixTime ?? position.ServerTime);
            hours += (long)(curTime - lastTime).TotalMilliseconds;
        }
        if (hours != 0)
        {
            position.Set(Position.KeyHours, hours);
        }
    }
}
