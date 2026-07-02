namespace Traccar.Model.Reports;

public class DeviceReportItem(Device device, Position? position)
{
    public Device Device { get; set; } = device;
    public Position? Position { get; set; } = position;
}
