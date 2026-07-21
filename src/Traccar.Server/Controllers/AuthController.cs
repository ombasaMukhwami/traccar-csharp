using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Server.Auth;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AuthEndpoints "/auth/login" and "/auth/refresh" — bearer-token login for API
/// clients (e.g. the Blazor fleet-management frontend), alongside <see cref="SessionController"/>'s
/// cookie-based login for traccar's own web UI. Unlike the mock, passwords are actually checked
/// and refresh tokens are persisted (see <see cref="RefreshToken"/>) rather than held in an
/// in-process dictionary, so they survive a restart and can be individually revoked.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("auth")]
public class AuthController(TraccarDbContext db, JwtIssuer jwtIssuer, JwtOptions jwtOptions) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(AuthLoginRequest request)
    {
        if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new TokenResponse { Error = "invalid_request", ErrorDescription = "Username and password are required." });
        }

        var reseller = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ResellerId && c.ParentId == null);
        if (reseller == null)
        {
            return BadRequest(new TokenResponse { Error = "invalid_reseller", ErrorDescription = "Reseller not found." });
        }

        // Traccar logins are keyed by Email, which — like the Blazor app's per-reseller usernames
        // — is scoped to ResellerId here so the same address can exist under different resellers.
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == request.UserName.ToLower() && u.ResellerId == request.ResellerId);

        if (user == null || user.Disabled || user.IsLockedOut || !user.IsPasswordValid(request.Password))
        {
            return Unauthorized(new TokenResponse { Error = "invalid_username_or_password", ErrorDescription = "Invalid username or password." });
        }

        return await IssueTokenPairAsync(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(AuthRefreshRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new TokenResponse { Error = "invalid_request", ErrorDescription = "Refresh token is required." });
        }

        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken);
        if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized(new TokenResponse { Error = "invalid_grant", ErrorDescription = "Unknown or expired refresh token." });
        }

        var user = await db.Users.FindAsync(stored.UserId);
        if (user == null)
        {
            return Unauthorized(new TokenResponse { Error = "invalid_grant", ErrorDescription = "User no longer exists." });
        }

        db.RefreshTokens.Remove(stored); // single-use: rotate on every refresh
        return await IssueTokenPairAsync(user);
    }

    private async Task<TokenResponse> IssueTokenPairAsync(User user)
    {
        var accessToken = jwtIssuer.IssueAccessToken(user);
        var refreshToken = JwtIssuer.IssueRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(jwtOptions.RefreshTokenLifetimeDays),
        });
        await db.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = jwtOptions.AccessTokenLifetimeSeconds,
            TokenType = "Bearer",
        };
    }
}

public sealed class AuthLoginRequest
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int ResellerId { get; set; }
}

public sealed class AuthRefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>OAuth2 token endpoint response shape (RFC 6749 section 5.1 / 5.2).</summary>
public sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}
