using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>Port of MockApi's AdministrativeEndpoints "Agents" routes.</summary>
[ApiController]
[Authorize]
[Route("api/agents")]
public class AgentsController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AgentDetails>>> Get([FromQuery] int resellerId) =>
        await db.Agents.Where(a => a.ResellerId == resellerId).OrderBy(a => a.Id).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AgentDetails>> GetById(int id)
    {
        var agent = await db.Agents.FindAsync(id);
        return agent == null ? NotFound() : agent;
    }

    [HttpPost]
    public async Task<ActionResult<AgentDetails>> Create(AgentDetails agent)
    {
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, agent);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AgentDetails agent)
    {
        var existing = await db.Agents.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }
        agent.Id = id;
        db.Entry(existing).CurrentValues.SetValues(agent);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent == null)
        {
            return NotFound();
        }
        db.Agents.Remove(agent);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
