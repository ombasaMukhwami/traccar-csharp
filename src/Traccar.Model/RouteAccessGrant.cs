namespace Traccar.Model;

/// <summary>
/// One user's access grant for a single route — independent View/Add/Edit/Delete rights,
/// assigned directly per-user. A route the user has no access to simply isn't in their
/// User.RouteAccess list at all.
/// </summary>
public class RouteAccessGrant
{
    public string Path { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanAdd { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}
