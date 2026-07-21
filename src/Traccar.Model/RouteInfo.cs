namespace Traccar.Model;

/// <summary>
/// The fixed, non-admin-editable catalog of gated routes/pages in a client application — drives
/// the "Route Access" grid consumers use to build <see cref="RouteAccessGrant"/> lists. Grants are
/// keyed by <see cref="Path"/>, not <see cref="Id"/>.
/// </summary>
public class RouteInfo
{
    public int Id { get; set; }

    /// <summary>No leading slash — e.g. "admin/users", "map".</summary>
    public string Path { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Groups rows in the Route Access grid, e.g. "General" or "Administrative".</summary>
    public string GroupName { get; set; } = string.Empty;

    public bool SupportsAdd { get; set; }

    public bool SupportsEdit { get; set; }

    public bool SupportsDelete { get; set; }

    /// <summary>The fixed catalog of gated routes for this app's admin UI, seeded once via
    /// <c>HasData</c>.</summary>
    public static readonly IReadOnlyList<RouteInfo> Catalog =
    [
        new() { Id = 1, Path = "map", Label = "Fleet", GroupName = "General" },
        new() { Id = 2, Path = "dashboard", Label = "Dashboard", GroupName = "General" },
        new() { Id = 3, Path = "geozones", Label = "Geozones", GroupName = "General", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 4, Path = "alerts", Label = "Alerts", GroupName = "General", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 5, Path = "trips", Label = "History", GroupName = "General" },
        new() { Id = 6, Path = "admin/users", Label = "Users", GroupName = "Administrative", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 7, Path = "admin/devices", Label = "Devices", GroupName = "Administrative", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 8, Path = "admin/move-devices", Label = "Move Devices", GroupName = "Administrative", SupportsEdit = true },
        new() { Id = 9, Path = "admin/sim-cards", Label = "SIM Cards", GroupName = "Administrative", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 10, Path = "admin/clients", Label = "Clients", GroupName = "Administrative", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 11, Path = "admin/agents", Label = "Agents", GroupName = "Administrative", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 12, Path = "admin/resellers", Label = "Resellers", GroupName = "Administrative", SupportsAdd = true, SupportsEdit = true, SupportsDelete = true },
        new() { Id = 13, Path = "admin/database", Label = "Database", GroupName = "Administrative" },
    ];
}
