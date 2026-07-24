namespace Traccar.Model;

/// <summary>Port of the Blazor frontend's DashboardStat DTO — per-client online/offline device
/// counts, from administrative/v1/api/Dashboard/stats.</summary>
public class DashboardStat
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Online { get; set; }

    public int Offline { get; set; }
}
