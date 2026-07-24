namespace Traccar.Model;

/// <summary>
/// Fleet-management admin-screen projection of <see cref="User"/> — port of the Blazor frontend's
/// AppUser DTO. Not a persisted entity of its own; AccountController projects it from User.
/// <see cref="Id"/> is a string (matching the frontend's shared DTO shape) even though
/// <see cref="User.Id"/> is a long underneath.
/// </summary>
public class AppUser
{
    public string? Id { get; set; }

    /// <summary>Maps to <see cref="User.Email"/> — this app's login identifier.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Maps to <see cref="User.Name"/>.</summary>
    public string? FullName { get; set; }

    public string? Email { get; set; }

    /// <summary>Maps to <see cref="User.Phone"/>.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Inverse of <see cref="User.Disabled"/>.</summary>
    public bool IsEnabled { get; set; }

    public bool IsLockedOut { get; set; }

    public List<RouteAccessGrant> RouteAccess { get; set; } = [];

    /// <summary>Frontend shape allows multiple clients per user; <see cref="User.ClientId"/> only
    /// stores one, so this is always zero-or-one elements in practice.</summary>
    public List<int> ClientId { get; set; } = [];

    public int? ResellerId { get; set; }

    public string? CurrentPassword { get; set; }

    public string? NewPassword { get; set; }

    public string? ConfirmPassword { get; set; }
}
