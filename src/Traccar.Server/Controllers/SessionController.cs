using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

[ApiController]
[Route("api/session")]
public class SessionController(TraccarDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<User>> Login([FromForm] string email, [FromForm] string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || user.Disabled || !user.IsPasswordValid(password))
        {
            return Unauthorized();
        }
        return user;
    }

    [HttpDelete]
    public IActionResult Logout() => NoContent();
}
