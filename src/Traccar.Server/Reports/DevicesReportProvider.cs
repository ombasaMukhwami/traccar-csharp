using Traccar.Model.Reports;

namespace Traccar.Server.Reports;

public sealed class DevicesReportProvider(ReportUtils reportUtils)
{
    public async Task<List<DeviceReportItem>> GetObjectsAsync(long userId)
    {
        var latestPositions = await reportUtils.GetLatestPositionsAsync(userId);
        var devices = await reportUtils.GetAccessibleDevicesAsync(userId, [], []);

        return devices
            .Select(d => new DeviceReportItem(d, latestPositions.GetValueOrDefault(d.Id)))
            .ToList();
    }
}
