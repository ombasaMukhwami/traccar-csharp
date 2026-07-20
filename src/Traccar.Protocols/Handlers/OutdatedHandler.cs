using Traccar.Model;

namespace Traccar.Protocols.Handlers;

public sealed class OutdatedHandler
{
    // GPS epoch — positions with Outdated=true and no prior fix are anchored here.
    public static readonly DateTime GpsEpoch = new(1980, 1, 6, 0, 0, 0, DateTimeKind.Utc);

    public void Process(Position position, Position? last)
    {
        if (!position.Outdated) return;

        if (last != null)
        {
            position.FixTime = last.FixTime;
            position.Valid = last.Valid;
            position.Latitude = last.Latitude;
            position.Longitude = last.Longitude;
            position.Altitude = last.Altitude;
            position.Speed = last.Speed;
            position.Course = last.Course;
            position.Accuracy = last.Accuracy;
        }
        else
        {
            position.FixTime = GpsEpoch;
        }

        position.DeviceTime ??= position.ServerTime;
    }
}
