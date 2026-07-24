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

    /// <summary>Clients this user is assigned to — a user can work across several (e.g. a
    /// dispatcher managing multiple fleets), matching the Blazor fleet-management frontend's
    /// multi-select AppUser.ClientId. Non-administrators are scoped to devices whose
    /// Device.ClientId is in this list (see ReportUtils.GetAccessibleDevicesAsync); null/empty
    /// means unassigned and sees no devices. JwtIssuer emits one "client_id" JWT claim per entry
    /// — the frontend's ClientAccessClaims reads them back the same way.</summary>
    public List<int>? ClientId { get; set; }

    /// <summary>Reseller tenant that manages this user — distinct from <see cref="ClientId"/>,
    /// the client this user belongs to.</summary>
    public int? ResellerId { get; set; }

    public List<RouteAccessGrant>? RouteAccess { get; set; }

    /// <summary>See <see cref="Model.UserType"/>'s own doc comment.</summary>
    public UserType UserType { get; set; } = Model.UserType.User;

    /// <summary>The <see cref="RouteAccess"/> a newly created user of this <paramref name="userType"/>
    /// starts with — full access for <see cref="Model.UserType.Administrator"/>, everything except
    /// "admin/database" and "admin/resellers" for <see cref="Model.UserType.User"/>.</summary>
    public static List<RouteAccessGrant> DefaultRouteAccess(UserType userType) => RouteInfo.Catalog
        .Where(r => userType == Model.UserType.Administrator || r.Path is not ("admin/database" or "admin/resellers"))
        .Select(r => new RouteAccessGrant
        {
            Path = r.Path,
            CanView = true,
            CanAdd = r.SupportsAdd,
            CanEdit = r.SupportsEdit,
            CanDelete = r.SupportsDelete,
        })
        .ToList();

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
