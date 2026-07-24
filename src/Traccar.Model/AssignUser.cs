namespace Traccar.Model;

/// <summary>Port of the Blazor frontend's AssignUser DTO — a narrower operation than a full user
/// edit, assigning a user to one or more clients.</summary>
public class AssignUser
{
    public string? UserId { get; set; }

    public List<int> ClientIds { get; set; } = [];
}
