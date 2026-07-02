namespace Traccar.Model.Reports;

public class DeviceReportSection
{
    public string DeviceName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;

    private List<object> _objects = [];

    public IReadOnlyList<object> Objects => _objects;

    public void SetObjects<T>(IEnumerable<T> items) where T : class =>
        _objects = items.Cast<object>().ToList();
}
