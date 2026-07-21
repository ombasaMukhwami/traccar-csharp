using Microsoft.Extensions.Configuration;

namespace Traccar.Server;

/// <summary>
/// JWT bearer settings — bound from the "Jwt" section in appsettings.json. Serves API clients
/// (e.g. the Blazor fleet-management frontend) that use bearer tokens instead of the cookie
/// session; see <see cref="Controllers.AuthController"/> and Program.cs's dual auth-scheme setup.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Symmetric signing key (HS256). Null/empty generates a random ephemeral key at
    /// startup — fine for local development, but tokens (and outstanding refresh tokens) won't
    /// survive a restart, and multiple server instances won't agree on a key. Set this in
    /// production.</summary>
    public string? Secret { get; set; }

    public string Issuer { get; set; } = "traccar";

    public string Audience { get; set; } = "traccar-clients";

    public int AccessTokenLifetimeSeconds { get; set; } = 3600;

    public int RefreshTokenLifetimeDays { get; set; } = 30;

    public static JwtOptions Bind(IConfiguration configuration)
    {
        var options = new JwtOptions();
        configuration.GetSection(SectionName).Bind(options);
        return options;
    }
}
