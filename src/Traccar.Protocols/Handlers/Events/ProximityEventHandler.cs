using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Protocols.Handlers.Events;

public sealed class ProximityEventHandler(
    IDbContextFactory<TraccarDbContext> dbContextFactory,
    PositionCache positionCache,
    ILogger<ProximityEventHandler> logger) : BaseEventHandler(logger)
{
    protected override async ValueTask OnPositionAsync(Position position, Position? last, Action<Event> callback)
    {
        // Only process the most recent fix for this device — skip historical replays.
        if (last != null && position.FixTime.GetValueOrDefault() < last.FixTime.GetValueOrDefault())
            return;

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var device = await db.Devices.FindAsync(position.DeviceId);
        if (device == null)
            return;

        // "Linked devices" are other devices sharing the same non-zero group.
        var linkedDevices = device.GroupId == 0
            ? []
            : await db.Devices
                .Where(d => d.GroupId == device.GroupId && d.Id != device.Id)
                .ToListAsync();

        if (linkedDevices.Count == 0)
            return;

        double enterDistance = device.GetDouble("event.proximityEnterDistance", 200.0);
        double exitDistance = device.GetDouble("event.proximityExitDistance", 200.0);
        double unaccompaniedDistance = device.GetDouble("event.unaccompaniedDistance", 0.0);

        bool checkUnaccompanied = unaccompaniedDistance > 0
            && last != null
            && !last.GetBoolean(Position.KeyMotion)
            && position.GetBoolean(Position.KeyMotion);

        if (enterDistance <= 0 && exitDistance <= 0 && !checkUnaccompanied)
            return;

        double maxDistance = Math.Max(Math.Max(enterDistance, exitDistance), unaccompaniedDistance);
        double latDelta = DistanceCalculator.GetLatitudeDelta(maxDistance);
        double lonDelta = DistanceCalculator.GetLongitudeDelta(maxDistance, position.Latitude);
        bool anyAccompanied = false;

        foreach (var linked in linkedDevices)
        {
            var linkedPos = positionCache.GetLastPosition(linked.Id);
            if (linkedPos == null)
                continue;

            double distNew = BoundedDistance(position, linkedPos, latDelta, lonDelta);
            // When no prior position exists treat the device as having been infinitely far away.
            double distOld = last != null
                ? BoundedDistance(last, linkedPos, latDelta, lonDelta)
                : double.PositiveInfinity;

            if (enterDistance > 0 && distOld > enterDistance && distNew <= enterDistance)
            {
                var ev = new Event(Event.TypeProximityEnter, position);
                ev.Set("linkedDeviceId", linked.Id);
                callback(ev);
            }
            else if (exitDistance > 0 && distOld <= exitDistance && distNew > exitDistance)
            {
                var ev = new Event(Event.TypeProximityExit, position);
                ev.Set("linkedDeviceId", linked.Id);
                callback(ev);
            }

            if (distNew <= unaccompaniedDistance)
                anyAccompanied = true;
        }

        if (checkUnaccompanied && !anyAccompanied)
            callback(new Event(Event.TypeUnaccompaniedMotion, position));
    }

    private static double BoundedDistance(Position from, Position to, double latDelta, double lonDelta)
    {
        if (Math.Abs(from.Latitude - to.Latitude) > latDelta
            || Math.Abs(from.Longitude - to.Longitude) > lonDelta)
            return double.PositiveInfinity;
        return DistanceCalculator.Distance(from, to);
    }
}
