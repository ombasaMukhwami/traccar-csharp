using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Port of MockApi's AdministrativeEndpoints "Agents" routes, at the exact paths the Blazor
/// fleet-management frontend's AdminClientsService calls — distinct from
/// <see cref="AgentsController"/>'s "api/agents" convention, which nothing in this frontend
/// actually calls. <see cref="AgentDetails"/> is an exact field-for-field match with the
/// frontend's own model, so no projection is needed here.
///
/// Deliberately <see cref="AllowAnonymousAttribute"/> — see
/// <see cref="AdministrativeClientsController"/>'s doc comment for why.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("administrative/v1/api/Agent/agents")]
public class AdministrativeAgentsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet("{resellerId:int}")]
    public async Task<ActionResult<List<AgentDetails>>> Get(int resellerId) =>
        await db.Agents.Where(a => a.ResellerId == resellerId).OrderBy(a => a.Id).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<AgentDetails>> Create(AgentDetails agent)
    {
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AgentDetails>> Update(int id, AgentDetails agent)
    {
        var existing = await db.Agents.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }
        agent.Id = id;
        db.Entry(existing).CurrentValues.SetValues(agent);
        await db.SaveChangesAsync();
        return existing;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent != null)
        {
            db.Agents.Remove(agent);
            await db.SaveChangesAsync();
        }
        return Ok();
    }
}
