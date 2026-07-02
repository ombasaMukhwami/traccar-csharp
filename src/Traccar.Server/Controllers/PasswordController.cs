using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Mirrors Java's PasswordResource. Both endpoints are public ([AllowAnonymous]) since the user
/// has no session when resetting their password.
///
/// Token mechanism: ITimeLimitedDataProtector creates a cryptographically-signed, time-limited
/// token containing the userId — no separate token table needed.
///
/// Email sending: Java uses MailManager + Velocity templates. This port has no mail system
/// (same scope decision as no SMS). On reset, the token is logged so it can be retrieved
/// via server logs during development; wire up a real IEmailSender to send it in production.
/// </summary>
[ApiController]
[Route("api/password")]
public class PasswordController(
    TraccarDbContext db,
    IDataProtectionProvider protection,
    ILogger<PasswordController> logger) : ControllerBase
{
    private const string Purpose = "traccar.password-reset";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private ITimeLimitedDataProtector Protector =>
        protection.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    // -------------------------------------------------------------------------
    // POST /api/password/reset
    // Looks up the user by email and generates a time-limited reset token.
    // Always returns 200 to prevent user-enumeration attacks.
    // -------------------------------------------------------------------------

    [HttpPost("reset")]
    [AllowAnonymous]
    public async Task<IActionResult> Reset([FromForm] string email)
    {
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == email.ToLower());

        if (user != null)
        {
            var token = Protector.Protect(user.Id.ToString(), DateTimeOffset.UtcNow.Add(TokenLifetime));
            // No mail system in this port — log the token so it is available during development.
            // In production, replace this with a real email send using the token as a query param:
            //   <base_url>/password-reset?token=<token>
            logger.LogInformation("Password reset token for user {UserId} ({Email}): {Token}",
                user.Id, user.Email, token);
        }

        return Ok();
    }

    // -------------------------------------------------------------------------
    // POST /api/password/update
    // Verifies the reset token and updates the user's password.
    // -------------------------------------------------------------------------

    [HttpPost("update")]
    [AllowAnonymous]
    public async Task<IActionResult> Update([FromForm] string token, [FromForm] string password)
    {
        long userId;
        try
        {
            var payload = Protector.Unprotect(token, out var expiry);
            if (DateTimeOffset.UtcNow > expiry)
            {
                return BadRequest("Token has expired.");
            }
            userId = long.Parse(payload);
        }
        catch
        {
            return BadRequest("Invalid or expired token.");
        }

        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        user.SetPassword(password);
        await db.SaveChangesAsync();
        return Ok();
    }
}
