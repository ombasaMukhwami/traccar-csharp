using Traccar.Model.Reports;

namespace Traccar.Server.Reports;

public sealed class StopsReportProvider(ReportUtils reportUtils)
{
    public async Task<List<StopReportItem>> GetObjectsAsync(
        long userId, IList<long> deviceIds, IList<long> groupIds, DateTime from, DateTime to)
    {
        reportUtils.CheckPeriodLimit(from, to);

        var result = new List<StopReportItem>();
        foreach (var device in await reportUtils.GetAccessibleDevicesAsync(userId, deviceIds, groupIds))
            result.AddRange(await reportUtils.DetectTripsAndStopsAsync<StopReportItem>(device, from, to));

        return result;
    }
}
