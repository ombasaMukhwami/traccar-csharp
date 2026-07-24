using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Traccar.Model;
using Traccar.Protocols;

namespace Traccar.Server.Auth;

/// <summary>
/// Mints access tokens for bearer-auth API clients. Claim shape (sub/name/email/phone_number/
/// resellerid/client_id/route_access) matches what the Blazor fleet-management frontend's
/// AccessTokenClaimsFactory expects when decoding the token client-side, so it keeps working
/// unmodified against this server. <see cref="ClaimTypes.Role"/> is added on top purely for our
/// own [Authorize(Roles = ...)] checks — the frontend ignores it.
/// </summary>
public class JwtIssuer(JwtOptions options, SymmetricSecurityKey signingKey)
{
    public string IssueAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("name", user.Name ?? user.Email),
            new("email", user.Email),
            new("phone_number", user.Phone ?? ""),
            new("resellerid", (user.ResellerId ?? 0).ToString()),
        };

        if (user.Administrator)
        {
            claims.Add(new Claim(ClaimTypes.Role, ConfigKeys.Auth.RoleAdministrator));
        }

        // One "client_id" claim per assigned client — the frontend's ClientAccessClaims collects
        // them all back into a set, not just the first.
        claims.AddRange((user.ClientId ?? []).Select(id => new Claim("client_id", id.ToString())));

        // One claim per View-granted route only — a route without View is unreachable regardless
        // of its other flags. Format: "{path}:V{A}{E}{D}", e.g. "admin/users:VAED", "map:V".
        claims.AddRange((user.RouteAccess ?? [])
            .Where(g => g.CanView)
            .Select(g => new Claim(
                "route_access",
                $"{g.Path}:V{(g.CanAdd ? "A" : "")}{(g.CanEdit ? "E" : "")}{(g.CanDelete ? "D" : "")}")));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddSeconds(options.AccessTokenLifetimeSeconds),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string IssueRefreshToken() => Guid.NewGuid().ToString("N");
}
