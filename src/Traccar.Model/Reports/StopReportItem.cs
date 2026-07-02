namespace Traccar.Model.Reports;

public class StopReportItem : BaseReportItem
{
    public long PositionId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public long Duration { get; set; }    // milliseconds
    public long EngineHours { get; set; } // milliseconds
}
