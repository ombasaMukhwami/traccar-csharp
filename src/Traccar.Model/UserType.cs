namespace Traccar.Model;

/// <summary>
/// Not exposed on the user-editor UI at all — every user created through it always gets the
/// default (<see cref="User"/>); only the seeded admin account (see DatabaseSeeder) is ever
/// <see cref="Administrator"/>. Purely a preset for the <see cref="Model.RouteAccessGrant"/> list
/// a new user gets at creation time (see <see cref="Model.User.DefaultRouteAccess"/>) — distinct
/// from <see cref="Model.User.Administrator"/>, which drives actual authorization (JWT role
/// claim, [Authorize(Roles=...)], the ClientId-scoping bypass) and is unrelated to this field.
/// </summary>
public enum UserType
{
    User,
    Administrator,
}
