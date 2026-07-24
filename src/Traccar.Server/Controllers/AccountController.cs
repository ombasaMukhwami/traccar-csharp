using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AccountEndpoints "users/roles/permissions/assign" routes, at the exact paths
/// the Blazor fleet-management frontend's AccountApiClient calls. Backs user administration and
/// the self-service Profile page.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why. "users/me" still resolves
/// the real signed-in user from the bearer token's claims when one is present (ASP.NET Core
/// populates HttpContext.User from a valid token regardless of whether the endpoint requires
/// authorization) rather than MockApi's hardcoded "always user 1".
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/account")]
public class AccountController(TraccarDbContext db) : ControllerBase
{
    private static AppUser ToAppUser(User u) => new()
    {
        Id = u.Id.ToString(),
        UserName = u.Email,
        FullName = u.Name,
        Email = u.Email,
        PhoneNumber = u.Phone,
        IsEnabled = !u.Disabled,
        IsLockedOut = u.IsLockedOut,
        RouteAccess = u.RouteAccess ?? [],
        ClientId = u.ClientId ?? [],
        ResellerId = u.ResellerId,
    };

    [HttpGet("users")]
    public async Task<ActionResult<List<AppUser>>> GetUsers() =>
        (await db.Users.OrderBy(u => u.Id).ToListAsync()).Select(ToAppUser).ToList();

    [HttpGet("users/me")]
    public async Task<ActionResult<AppUser?>> GetCurrentUser()
    {
        // 200/null rather than 404 when there's no identity yet (Blazor Server's static
        // prerender pass calls this before the stored bearer token is available via
        // ProtectedLocalStorage) — Profile/Index.razor already treats a null _user as "not
        // loaded", but EnsureSuccessStatusCode() on a non-2xx crashes the whole circuit, and the
        // real user loads fine once the interactive circuit reconnects and retries with a token.
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return Ok((AppUser?)null);
        }
        var user = await db.Users.FindAsync(id);
        return Ok(user == null ? null : ToAppUser(user));
    }

    [HttpPost("users")]
    public async Task<ActionResult<AppUser>> Create(AppUser dto)
    {
        if (dto.ClientId.Count == 0)
        {
            return BadRequest("A user must be assigned to at least one client.");
        }

        // UserType has no UI control — every user created here is always the default (User),
        // which also decides the RouteAccess they start with (see User.DefaultRouteAccess).
        // Only the seeded admin account (DatabaseSeeder) is ever UserType.Administrator.
        var user = new User
        {
            Email = dto.Email ?? dto.UserName,
            Name = dto.FullName ?? dto.UserName,
            Phone = dto.PhoneNumber,
            Disabled = !dto.IsEnabled,
            UserType = UserType.User,
            RouteAccess = Traccar.Model.User.DefaultRouteAccess(UserType.User),
            ClientId = dto.ClientId,
            ResellerId = dto.ResellerId,
        };
        user.SetPassword(string.IsNullOrEmpty(dto.NewPassword) ? Guid.NewGuid().ToString("N") : dto.NewPassword);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return ToAppUser(user);
    }

    [HttpPut("users/{id}")]
    public async Task<ActionResult<AppUser>> Update(string id, AppUser dto)
    {
        if (!long.TryParse(id, out var userId))
        {
            return NotFound();
        }
        if (dto.ClientId.Count == 0)
        {
            return BadRequest("A user must be assigned to at least one client.");
        }
        var existing = await db.Users.FindAsync(userId);
        if (existing == null)
        {
            return NotFound();
        }

        existing.Email = dto.Email ?? dto.UserName;
        existing.Name = dto.FullName ?? dto.UserName;
        existing.Phone = dto.PhoneNumber;
        existing.Disabled = !dto.IsEnabled;
        existing.RouteAccess = dto.RouteAccess;
        existing.ClientId = dto.ClientId;
        existing.ResellerId = dto.ResellerId;

        if (!string.IsNullOrEmpty(dto.NewPassword))
        {
            existing.SetPassword(dto.NewPassword);
        }

        await db.SaveChangesAsync();
        return ToAppUser(existing);
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (long.TryParse(id, out var userId))
        {
            var existing = await db.Users.FindAsync(userId);
            if (existing != null)
            {
                db.Users.Remove(existing);
                await db.SaveChangesAsync();
            }
        }
        return Ok();
    }

    [HttpPut("users/unblock/{id}")]
    public async Task<IActionResult> Unblock(string id)
    {
        if (long.TryParse(id, out var userId))
        {
            var user = await db.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsLockedOut = false;
                await db.SaveChangesAsync();
            }
        }
        return Ok();
    }

    [HttpPost("assignuser")]
    public async Task<ActionResult<AssignUser>> AssignUser(AssignUser assignment)
    {
        if (assignment.ClientIds.Count == 0)
        {
            return BadRequest("A user must be assigned to at least one client.");
        }
        if (long.TryParse(assignment.UserId, out var userId))
        {
            var user = await db.Users.FindAsync(userId);
            if (user != null)
            {
                user.ClientId = assignment.ClientIds;
                await db.SaveChangesAsync();
            }
        }
        return assignment;
    }

    /// <summary>Read-only — the fixed route catalog isn't admin-editable, it mirrors the app's
    /// actual page inventory. Used by UserEditorDialog.razor's Route Access grid.</summary>
    [HttpGet("routes")]
    public async Task<ActionResult<List<RouteInfo>>> GetRoutes() =>
        await db.Routes.OrderBy(r => r.Id).ToListAsync();
}
