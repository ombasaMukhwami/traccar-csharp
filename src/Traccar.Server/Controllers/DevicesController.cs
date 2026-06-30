using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController(TraccarDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Device>>> Get([FromQuery] string? uniqueId, [FromQuery] long id = 0)
    {
        var query = db.Devices.AsQueryable();
        if (!string.IsNullOrEmpty(uniqueId))
        {
            query = query.Where(d => d.UniqueId == uniqueId);
        }
        if (id > 0)
        {
            query = query.Where(d => d.Id == id);
        }
        return await query.OrderBy(d => d.Name).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Device>> GetById(long id)
    {
        var device = await db.Devices.FindAsync(id);
        return device == null ? NotFound() : device;
    }

    [HttpPost]
    public async Task<ActionResult<Device>> Create(Device device)
    {
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, Device device)
    {
        if (id != device.Id)
        {
            return BadRequest();
        }
        db.Entry(device).State = EntityState.Modified;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var device = await db.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }
        db.Devices.Remove(device);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
