using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Protocols;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(TraccarDbContext db) : ControllerBase
{
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole(ConfigKeys.Auth.RoleAdministrator);

    [HttpGet]
    public async Task<ActionResult<List<User>>> Get()
    {
        if (IsAdmin)
        {
            return await db.Users.OrderBy(u => u.Name).ToListAsync();
        }
        var user = await db.Users.FindAsync(CurrentUserId);
        return user == null ? NotFound() : new List<User> { user };
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<User>> GetById(long id)
    {
        if (!IsAdmin && id != CurrentUserId)
        {
            return Forbid();
        }
        var user = await db.Users.FindAsync(id);
        return user == null ? NotFound() : user;
    }

    [HttpPost]
    [Authorize(Roles = ConfigKeys.Auth.RoleAdministrator)]
    public async Task<ActionResult<User>> Create([FromBody] UserDto dto)
    {
        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            Administrator = dto.Administrator,
            Readonly = dto.Readonly,
            Disabled = dto.Disabled,
        };
        user.SetPassword(dto.Password ?? throw new ArgumentException("Password is required"));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UserDto dto)
    {
        if (!IsAdmin && id != CurrentUserId)
        {
            return Forbid();
        }

        var user = await db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.Phone = dto.Phone;

        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.SetPassword(dto.Password);
        }

        // Only admins can promote/demote or disable
        if (IsAdmin)
        {
            user.Administrator = dto.Administrator;
            user.Readonly = dto.Readonly;
            user.Disabled = dto.Disabled;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = ConfigKeys.Auth.RoleAdministrator)]
    public async Task<IActionResult> Delete(long id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:long}/unblock")]
    [Authorize(Roles = ConfigKeys.Auth.RoleAdministrator)]
    public async Task<IActionResult> Unblock(long id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        user.IsLockedOut = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("routes")]
    public ActionResult<IReadOnlyList<RouteInfo>> GetRoutes() => Ok(RouteInfo.Catalog);
}

public sealed record UserDto(
    string? Name,
    string Email,
    string? Phone,
    string? Password,
    bool Administrator,
    bool Readonly,
    bool Disabled);
