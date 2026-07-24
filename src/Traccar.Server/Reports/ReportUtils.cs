using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Traccar.Model;
using Traccar.Model.Reports;
using Traccar.Protocols;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Server.Reports;

/// <summary>
/// Shared report logic: position queries, accessible-device resolution, trip/stop detection,
/// fuel calculation.  Mirrors Java's reports.common.ReportUtils (minus JXLS/geocoder concerns).
/// </summary>
public sealed class ReportUtils(TraccarDbContext db, IConfiguration configuration)
{
    // -------------------------------------------------------------------------
    // Period guard
    // -------------------------------------------------------------------------

    public void CheckPeriodLimit(DateTime from, DateTime to)
    {
        long limitSeconds = configuration.GetValue(ConfigKeys.Report.PeriodLimit, 0L);
        if (limitSeconds > 0 && (to - from).TotalSeconds > limitSeconds)
            throw new ArgumentException("Time period exceeds the configured limit");
    }

    // -------------------------------------------------------------------------
    // Position queries (mirror Java's PositionUtil)
    // -------------------------------------------------------------------------

    public async Task<List<Position>> GetPositionsAsync(long deviceId, DateTime from, DateTime to) =>
        await db.Positions
            .Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to)
            .OrderBy(p => p.FixTime)
            .ToListAsync();

    public async Task<Position?> GetEdgePositionAsync(long deviceId, DateTime from, DateTime to, bool last)
    {
        var q = db.Positions.Where(p => p.DeviceId == deviceId && p.FixTime >= from && p.FixTime <= to);
        return last
            ? await q.OrderByDescending(p => p.FixTime).FirstOrDefaultAsync()
            : await q.OrderBy(p => p.FixTime).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Returns distance between two positions in metres. Prefers odometer delta when
    /// useOdometer is true, falls back to totalDistance delta (mirrors Java PositionUtil).
    /// </summary>
    public static double CalculateDistance(Position first, Position last, bool useOdometer)
    {
        double firstOdometer = first.GetDouble(Position.KeyOdometer);
        double lastOdometer = last.GetDouble(Position.KeyOdometer);

        if (useOdometer && firstOdometer != 0 && lastOdometer != 0)
            return lastOdometer - firstOdometer;

        return last.GetDouble(Position.KeyTotalDistance) - first.GetDouble(Position.KeyTotalDistance);
    }

    // -------------------------------------------------------------------------
    // Accessible-device resolution — scoped by Device.ClientId against the caller's own
    // User.ClientId list (administrators see every device, matching the pre-existing convention
    // elsewhere that admins bypass ownership checks).
    // -------------------------------------------------------------------------

    private async Task<IQueryable<Device>> AccessibleDevicesQueryAsync(long userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is { Administrator: true })
        {
            return db.Devices;
        }

        var clientIds = user?.ClientId ?? [];
        return db.Devices.Where(d => clientIds.Contains(d.ClientId));
    }

    public async Task<List<Device>> GetAccessibleDevicesAsync(long userId, IList<long> deviceIds)
    {
        var accessible = await AccessibleDevicesQueryAsync(userId);

        if (deviceIds.Count > 0)
        {
            accessible = accessible.Where(d => deviceIds.Contains(d.Id));
        }

        return await accessible.ToListAsync();
    }

    public async Task<Dictionary<long, Position>> GetLatestPositionsAsync(long userId)
    {
        var accessible = await AccessibleDevicesQueryAsync(userId);

        // Latest position per device via the PositionId stored on the device row
        return await accessible
            .Where(d => d.PositionId != null && d.PositionId != 0)
            .Join(db.Positions, d => d.PositionId!.Value, p => p.Id, (_, p) => p)
            .ToDictionaryAsync(p => p.DeviceId);
    }

    // -------------------------------------------------------------------------
    // Fuel
    // -------------------------------------------------------------------------

    public static double CalculateFuel(Position first, Position last, Device device)
    {
        if (first.HasAttribute(Position.KeyFuelUsed) && last.HasAttribute(Position.KeyFuelUsed))
            return last.GetDouble(Position.KeyFuelUsed) - first.GetDouble(Position.KeyFuelUsed);

        if (first.HasAttribute(Position.KeyFuel) && last.HasAttribute(Position.KeyFuel))
            return first.GetDouble(Position.KeyFuel) - last.GetDouble(Position.KeyFuel);

        if (first.HasAttribute(Position.KeyFuelLevel) && last.HasAttribute(Position.KeyFuelLevel)
            && device.HasAttribute("fuelCapacity"))
        {
            return ((first.GetDouble(Position.KeyFuelLevel) - last.GetDouble(Position.KeyFuelLevel)) / 100)
                   * device.GetDouble("fuelCapacity");
        }

        return 0;
    }

    // -------------------------------------------------------------------------
    // Driver
    // -------------------------------------------------------------------------

    public static string? FindDriver(Position first, Position last) =>
        first.HasAttribute(Position.KeyDriverUniqueId)
            ? first.GetString(Position.KeyDriverUniqueId)
            : last.HasAttribute(Position.KeyDriverUniqueId)
                ? last.GetString(Position.KeyDriverUniqueId)
                : null;

    // -------------------------------------------------------------------------
    // Trip/stop detection (mirrors Java's detectTripsAndStops / slow+fast paths)
    // -------------------------------------------------------------------------

    public async Task<List<T>> DetectTripsAndStopsAsync<T>(Device device, DateTime from, DateTime to)
        where T : BaseReportItem, new()
    {
        long fastThreshold = configuration.GetValue(ConfigKeys.Report.FastThreshold, 86400L);
        return (to - from).TotalSeconds > fastThreshold
            ? await FastTripsAndStopsAsync<T>(device, from, to)
            : await SlowTripsAndStopsAsync<T>(device, from, to);
    }

    private async Task<List<T>> SlowTripsAndStopsAsync<T>(Device device, DateTime from, DateTime to)
        where T : BaseReportItem, new()
    {
        var result = new List<T>();
        var tripsConfig = new TripsConfig(configuration);
        bool ignoreOdometer = tripsConfig.IgnoreOdometer;
        bool lookingForTrips = typeof(T) == typeof(TripReportItem);

        var positions = await GetPositionsAsync(device.Id, from, to);
        if (positions.Count == 0)
            return result;

        // Detect motion events by replaying positions through MotionProcessor
        var events = new List<Event>();
        var positionMap = new Dictionary<long, Position>();

        bool initialMotion = positions[0].GetBoolean(Position.KeyMotion);
        Position? startPosition = initialMotion == lookingForTrips ? positions[0] : null;
        double maxSpeed = startPosition?.Speed ?? 0;

        var motionState = new MotionState
        {
            MotionStreak = initialMotion,
            Moving = initialMotion,
        };

        for (int i = 0; i < positions.Count; i++)
        {
            var position = positions[i];
            var lastPosition = i > 0 ? positions[i - 1] : null;

            maxSpeed = Math.Max(maxSpeed, position.Speed);
            positionMap[position.Id] = position;

            bool motion = position.GetBoolean(Position.KeyMotion);
            MotionProcessor.UpdateState(motionState, lastPosition, position, motion, tripsConfig);

            if (motionState.Event is not null)
            {
                motionState.Event.Set("maxSpeed", maxSpeed);
                events.Add(motionState.Event);
                maxSpeed = 0;
            }
        }

        // Replay events to build trip/stop segments
        startPosition = null;
        foreach (var ev in events)
        {
            bool isMoving = ev.Type == Event.TypeDeviceMoving;
            if (isMoving == lookingForTrips)
            {
                startPosition = positionMap.GetValueOrDefault(ev.PositionId);
            }
            else if (startPosition is not null && positionMap.TryGetValue(ev.PositionId, out var endPosition))
            {
                result.Add(BuildItem<T>(device, startPosition, endPosition,
                    ev.GetDouble("maxSpeed"), ignoreOdometer));
                startPosition = null;
            }
        }

        // Open segment reaching the last position
        if (startPosition is not null && positions.Count > 0)
        {
            result.Add(BuildItem<T>(device, startPosition, positions[^1], maxSpeed, ignoreOdometer));
        }

        return result;
    }

    private async Task<List<T>> FastTripsAndStopsAsync<T>(Device device, DateTime from, DateTime to)
        where T : BaseReportItem, new()
    {
        var result = new List<T>();
        var tripsConfig = new TripsConfig(configuration);
        bool ignoreOdometer = tripsConfig.IgnoreOdometer;
        bool lookingForTrips = typeof(T) == typeof(TripReportItem);

        // Read pre-computed motion events from DB
        var events = await db.Events
            .Where(e => e.DeviceId == device.Id
                        && e.EventTime >= from && e.EventTime <= to
                        && (e.Type == Event.TypeDeviceMoving || e.Type == Event.TypeDeviceStopped))
            .OrderBy(e => e.EventTime)
            .ToListAsync();

        Position? startPosition = await GetEdgePositionAsync(device.Id, from, to, last: false);
        if (startPosition is not null && !startPosition.GetBoolean(Position.KeyMotion))
            startPosition = null;

        foreach (var ev in events)
        {
            bool isMoving = ev.Type == Event.TypeDeviceMoving;
            if (isMoving == lookingForTrips)
            {
                startPosition = await db.Positions
                    .FirstOrDefaultAsync(p => p.DeviceId == device.Id && p.Id == ev.PositionId);
            }
            else if (startPosition is not null)
            {
                var endPosition = await db.Positions
                    .FirstOrDefaultAsync(p => p.DeviceId == device.Id && p.Id == ev.PositionId);
                if (endPosition is not null)
                {
                    result.Add(BuildItem<T>(device, startPosition, endPosition, 0, ignoreOdometer));
                }
                startPosition = null;
            }
        }

        if (startPosition is not null)
        {
            var endPosition = await GetEdgePositionAsync(device.Id, from, to, last: true);
            if (endPosition is not null)
                result.Add(BuildItem<T>(device, startPosition, endPosition, 0, ignoreOdometer));
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Item builders
    // -------------------------------------------------------------------------

    private static T BuildItem<T>(
        Device device, Position start, Position end, double maxSpeed, bool ignoreOdometer)
        where T : BaseReportItem, new()
    {
        return typeof(T) == typeof(TripReportItem)
            ? (T)(object)BuildTrip(device, start, end, maxSpeed, ignoreOdometer)
            : (T)(object)BuildStop(device, start, end, ignoreOdometer);
    }

    private static TripReportItem BuildTrip(
        Device device, Position start, Position end, double maxSpeed, bool ignoreOdometer)
    {
        long durationMs = (long)(end.FixTime!.Value - start.FixTime!.Value).TotalMilliseconds;
        double distance = CalculateDistance(start, end, !ignoreOdometer);

        var trip = new TripReportItem
        {
            DeviceId = device.Id,
            DeviceName = device.Name ?? string.Empty,
            StartPositionId = start.Id,
            StartLat = start.Latitude,
            StartLon = start.Longitude,
            StartTime = start.FixTime,
            StartAddress = start.Address,
            EndPositionId = end.Id,
            EndLat = end.Latitude,
            EndLon = end.Longitude,
            EndTime = end.FixTime,
            EndAddress = end.Address,
            Distance = distance,
            Duration = durationMs,
            MaxSpeed = maxSpeed,
            SpentFuel = CalculateFuel(start, end, device),
            DriverUniqueId = FindDriver(start, end),
        };

        if (durationMs > 0)
            trip.AverageSpeed = UnitsConverter.KnotsFromMps(distance * 1000.0 / durationMs);

        if (!ignoreOdometer
            && start.GetDouble(Position.KeyOdometer) != 0
            && end.GetDouble(Position.KeyOdometer) != 0)
        {
            trip.StartOdometer = start.GetDouble(Position.KeyOdometer);
            trip.EndOdometer = end.GetDouble(Position.KeyOdometer);
        }
        else
        {
            trip.StartOdometer = start.GetDouble(Position.KeyTotalDistance);
            trip.EndOdometer = end.GetDouble(Position.KeyTotalDistance);
        }

        return trip;
    }

    private static StopReportItem BuildStop(
        Device device, Position start, Position end, bool ignoreOdometer)
    {
        long durationMs = (long)(end.FixTime!.Value - start.FixTime!.Value).TotalMilliseconds;

        var stop = new StopReportItem
        {
            DeviceId = device.Id,
            DeviceName = device.Name ?? string.Empty,
            PositionId = start.Id,
            Latitude = start.Latitude,
            Longitude = start.Longitude,
            Address = start.Address,
            StartTime = start.FixTime,
            EndTime = end.FixTime,
            Duration = durationMs,
            SpentFuel = CalculateFuel(start, end, device),
        };

        if (start.HasAttribute(Position.KeyHours) && end.HasAttribute(Position.KeyHours))
            stop.EngineHours = end.GetLong(Position.KeyHours) - start.GetLong(Position.KeyHours);

        if (!ignoreOdometer
            && start.GetDouble(Position.KeyOdometer) != 0
            && end.GetDouble(Position.KeyOdometer) != 0)
        {
            stop.StartOdometer = start.GetDouble(Position.KeyOdometer);
            stop.EndOdometer = end.GetDouble(Position.KeyOdometer);
        }
        else
        {
            stop.StartOdometer = start.GetDouble(Position.KeyTotalDistance);
            stop.EndOdometer = end.GetDouble(Position.KeyTotalDistance);
        }

        return stop;
    }
}
