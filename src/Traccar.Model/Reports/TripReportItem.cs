namespace Traccar.Model.Reports;

public class TripReportItem : BaseReportItem
{
    public long StartPositionId { get; set; }
    public long EndPositionId { get; set; }
    public double StartLat { get; set; }
    public double StartLon { get; set; }
    public double EndLat { get; set; }
    public double EndLon { get; set; }
    public string? StartAddress { get; set; }
    public string? EndAddress { get; set; }
    public long Duration { get; set; } // milliseconds
    public string? DriverUniqueId { get; set; }
    public string? DriverName { get; set; }
}
