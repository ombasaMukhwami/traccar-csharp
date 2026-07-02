namespace Traccar.Model.Reports;

public class BaseReportItem
{
    public long DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public double Distance { get; set; }
    public void AddDistance(double distance) => Distance += distance;
    public double AverageSpeed { get; set; }
    public double MaxSpeed { get; set; }
    public double SpentFuel { get; set; }
    public double StartOdometer { get; set; }
    public double EndOdometer { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
