namespace Traccar.Model;

/// <summary>Port of the Blazor frontend's DashboardAlertNotification DTO — a row from
/// administrative/v1/api/Dashboard/recent-alerts. Backed by real <see cref="Event"/> alarm rows
/// (there is no separate user-defined "Alert" backend yet), so AlertName is the humanized alarm
/// key (e.g. "overspeed" -&gt; "Overspeed") rather than a user-chosen alert rule name.</summary>
public class DashboardAlertNotification
{
    public string AlertName { get; set; } = string.Empty;

    public string? AssetName { get; set; }

    public DateTime AlertTime { get; set; }

    public string? LocationText { get; set; }
}
