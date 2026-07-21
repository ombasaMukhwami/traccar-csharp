using System.Text.Json.Serialization;

namespace Traccar.Model;

public class User : ExtendedModel
{
    public string? Name { get; set; }

    private string _email = string.Empty;

    public string Email
    {
        get => _email;
        set => _email = value.Trim();
    }

    public string? Phone { get; set; }

    public bool Readonly { get; set; }

    public bool Administrator { get; set; }

    public bool Disabled { get; set; }

    /// <summary>Locked out after too many failed login attempts — cleared via the unblock
    /// endpoint, distinct from an administrator-set <see cref="Disabled"/>.</summary>
    public bool IsLockedOut { get; set; }

    public DateTime? ExpirationTime { get; set; }

    /// <summary>Owning client/reseller tenant. Non-administrators are scoped to devices with a
    /// matching Device.ClientId (see ReportUtils.GetAccessibleDevicesAsync); null/0 means
    /// unassigned and sees no devices.</summary>
    public int? ClientId { get; set; }

    /// <summary>Reseller tenant that manages this user — distinct from <see cref="ClientId"/>,
    /// the client this user belongs to.</summary>
    public int? ResellerId { get; set; }

    public List<RouteAccessGrant>? RouteAccess { get; set; }

    /// <summary>Transient password-change fields — never persisted (see
    /// TraccarDbContext.OnModelCreating, which ignores them for the User entity).</summary>
    public string? CurrentPassword { get; set; }

    public string? NewPassword { get; set; }

    public string? ConfirmPassword { get; set; }

    public void SetPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return;
        }
        var result = Hashing.CreateHash(password);
        HashedPassword = result.Hash;
        Salt = result.Salt;
    }

    [JsonIgnore]
    public string? HashedPassword { get; set; }

    [JsonIgnore]
    public string? Salt { get; set; }

    public bool IsPasswordValid(string password) => Hashing.ValidatePassword(password, HashedPassword, Salt);
}
