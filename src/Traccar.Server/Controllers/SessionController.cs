using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Protocols;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

[ApiController]
[Route("api/session")]
public class SessionController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<User>> Get()
    {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(id);
        return user == null ? Unauthorized() : user;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<User>> Login([FromForm] string email, [FromForm] string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || user.Disabled || !user.IsPasswordValid(password))
        {
            return Unauthorized();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };
        if (user.Administrator)
        {
            claims.Add(new Claim(ClaimTypes.Role, ConfigKeys.Auth.RoleAdministrator));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return user;
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
