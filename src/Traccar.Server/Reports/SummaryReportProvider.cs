using Microsoft.Extensions.Configuration;
using Traccar.Model;
using Traccar.Model.Reports;
using Traccar.Protocols;
using Traccar.Protocols.Helpers;

namespace Traccar.Server.Reports;

public sealed class SummaryReportProvider(ReportUtils reportUtils, IConfiguration configuration)
{
    public async Task<List<SummaryReportItem>> GetObjectsAsync(
        long userId, IList<long> deviceIds, IList<long> groupIds,
        DateTime from, DateTime to, bool daily)
    {
        reportUtils.CheckPeriodLimit(from, to);

        var result = new List<SummaryReportItem>();
        foreach (var device in await reportUtils.GetAccessibleDevicesAsync(userId, deviceIds, groupIds))
        {
            var deviceResults = await CalculateDeviceResultsAsync(device, from, to, daily);
            result.AddRange(deviceResults.Where(r => r.StartTime is not null && r.EndTime is not null));
        }
        return result;
    }

    private async Task<List<SummaryReportItem>> CalculateDeviceResultsAsync(
        Device device, DateTime from, DateTime to, bool daily)
    {
        var results = new List<SummaryReportItem>();

        if (daily)
        {
            var cursor = from.Date; // truncate to start of day UTC
            while (cursor.Date < to.Date)
            {
                var nextDay = cursor.AddDays(1);
                var item = await CalculateDeviceResultAsync(device, cursor, nextDay);
                if (item is not null)
                    results.Add(item);
                cursor = nextDay;
            }
        }

        var final = await CalculateDeviceResultAsync(device, daily ? from.Date.AddDays((from.Date - from.Date).Days) : from, to);
        if (final is not null)
            results.Add(final);

        return results;
    }

    private async Task<SummaryReportItem?> CalculateDeviceResultAsync(
        Device device, DateTime from, DateTime to)
    {
        long fastThreshold = configuration.GetValue(ConfigKeys.Report.FastThreshold, 86400L);
        bool fast = (to - from).TotalSeconds > fastThreshold;

        var result = new SummaryReportItem
        {
            DeviceId = device.Id,
            DeviceName = device.Name ?? string.Empty,
        };

        Position? first = null;
        Position? last = null;

        if (fast)
        {
            first = await reportUtils.GetEdgePositionAsync(device.Id, from, to, last: false);
            last = await reportUtils.GetEdgePositionAsync(device.Id, from, to, last: true);
        }
        else
        {
            var positions = await reportUtils.GetPositionsAsync(device.Id, from, to);
            foreach (var position in positions)
            {
                first ??= position;
                if (position.Speed > result.MaxSpeed)
                    result.MaxSpeed = position.Speed;
                last = position;
            }
        }

        if (first is null || last is null)
            return null;

        bool ignoreOdometer = configuration.GetValue(ConfigKeys.Report.Trip.IgnoreOdometer, false);
        result.Distance = ReportUtils.CalculateDistance(first, last, !ignoreOdometer);
        result.SpentFuel = ReportUtils.CalculateFuel(first, last, device);

        if (first.HasAttribute(Position.KeyHours) && last.HasAttribute(Position.KeyHours))
        {
            result.StartHours = first.GetLong(Position.KeyHours);
            result.EndHours = last.GetLong(Position.KeyHours);
            long engineHours = result.EngineHours;
            if (engineHours > 0)
                result.AverageSpeed = UnitsConverter.KnotsFromMps(result.Distance * 1000.0 / engineHours);
        }

        if (!ignoreOdometer
            && first.GetDouble(Position.KeyOdometer) != 0
            && last.GetDouble(Position.KeyOdometer) != 0)
        {
            result.StartOdometer = first.GetDouble(Position.KeyOdometer);
            result.EndOdometer = last.GetDouble(Position.KeyOdometer);
        }
        else
        {
            result.StartOdometer = first.GetDouble(Position.KeyTotalDistance);
            result.EndOdometer = last.GetDouble(Position.KeyTotalDistance);
        }

        result.StartTime = first.FixTime;
        result.EndTime = last.FixTime;

        return result;
    }
}
