using Microsoft.AspNetCore.Mvc;

namespace Traccar.Server.Controllers;

[ApiController]
[Route("api/server")]
public class ServerController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get() => new
    {
        version = typeof(ServerController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
        serverTime = DateTime.UtcNow,
    };
}
