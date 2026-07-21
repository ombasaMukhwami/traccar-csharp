using Traccar.Model.Reports;

namespace Traccar.Server.Reports;

public sealed class TripsReportProvider(ReportUtils reportUtils)
{
    public async Task<List<TripReportItem>> GetObjectsAsync(
        long userId, IList<long> deviceIds, DateTime from, DateTime to)
    {
        reportUtils.CheckPeriodLimit(from, to);

        var result = new List<TripReportItem>();
        foreach (var device in await reportUtils.GetAccessibleDevicesAsync(userId, deviceIds))
            result.AddRange(await reportUtils.DetectTripsAndStopsAsync<TripReportItem>(device, from, to));

        return result;
    }
}
