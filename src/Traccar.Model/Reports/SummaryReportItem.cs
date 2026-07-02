namespace Traccar.Model.Reports;

public class SummaryReportItem : BaseReportItem
{
    private long _startHours;
    private long _endHours;

    public long StartHours
    {
        get => _startHours;
        set => _startHours = value;
    }

    public long EndHours
    {
        get => _endHours;
        set => _endHours = value;
    }

    public long EngineHours => _endHours - _startHours; // milliseconds
}
