using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Protocols;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Mirrors Java's CommandResource. Simplified relative to Java: no permission system exists yet in
/// this port (matching every other controller), no SMS/text-channel commands (no SmsManager
/// equivalent - see IProtocol.cs), and no offline command queue (QueuedCommand/CommandsManager's
/// broadcast-driven retry) - a command can only be sent to a currently-connected device, returning
/// an error otherwise rather than silently queuing it for later delivery.
/// </summary>
[ApiController]
[Authorize]
[Route("api/commands")]
public class CommandsController(TraccarDbContext db, ConnectionManager connectionManager, ServerManager serverManager) : ControllerBase
{
    private async Task<BaseProtocol?> GetDeviceProtocolAsync(long deviceId)
    {
        var device = await db.Devices.FindAsync(deviceId);
        if (device == null || device.PositionId == 0)
        {
            return null;
        }
        var position = await db.Positions.FindAsync(device.PositionId);
        return position?.Protocol != null ? serverManager.GetProtocol(position.Protocol) : null;
    }

    private bool TrySend(Command command)
    {
        var session = connectionManager.GetDeviceSession(command.DeviceId);
        if (session == null)
        {
            return false;
        }
        session.SendCommand(command);
        return true;
    }

    [HttpGet]
    public async Task<ActionResult<List<Command>>> Get([FromQuery] long deviceId = 0)
    {
        var query = db.Commands.AsQueryable();
        if (deviceId > 0)
        {
            query = query.Where(c => c.DeviceId == deviceId);
        }
        return await query.ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Command>> GetById(long id)
    {
        var command = await db.Commands.FindAsync(id);
        return command == null ? NotFound() : command;
    }

    [HttpPost]
    public async Task<ActionResult<Command>> Create(Command command)
    {
        db.Commands.Add(command);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = command.Id }, command);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, Command command)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }
        db.Entry(command).State = EntityState.Modified;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var command = await db.Commands.FindAsync(id);
        if (command == null)
        {
            return NotFound();
        }
        db.Commands.Remove(command);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lists this device's saved commands that its protocol can actually accept.</summary>
    [HttpGet("send")]
    public async Task<ActionResult<List<Command>>> GetSendable([FromQuery] long deviceId)
    {
        var protocol = await GetDeviceProtocolAsync(deviceId);
        var commands = await db.Commands.Where(c => c.DeviceId == deviceId).ToListAsync();

        return commands.Where(command => protocol != null
            ? !command.TextChannel && protocol.SupportedDataCommands.Contains(command.Type ?? string.Empty)
            : command.Type == Command.TypeCustom).ToList();
    }

    /// <summary>
    /// Sends a command immediately to a connected device (or every connected device in a group).
    /// If entity.Id refers to a saved command, its type/attributes are reused against the target device(s).
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] Command entity, [FromQuery] long groupId = 0)
    {
        if (entity.Id > 0)
        {
            var deviceId = entity.DeviceId;
            var saved = await db.Commands.FindAsync(entity.Id);
            if (saved == null)
            {
                return NotFound();
            }
            entity = saved;
            entity.DeviceId = deviceId;
        }

        if (groupId > 0)
        {
            var devices = await db.Devices.Where(d => d.GroupId == groupId).ToListAsync();
            var sentCount = devices.Count(device => TrySend(new Command
            {
                DeviceId = device.Id,
                Type = entity.Type,
                TextChannel = entity.TextChannel,
                Attributes = new Dictionary<string, object>(entity.Attributes),
            }));

            return sentCount > 0
                ? Ok(new { sent = sentCount, total = devices.Count })
                : Problem("No connected devices in group", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TrySend(entity))
        {
            return Problem("Device is not connected", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(entity);
    }

    /// <summary>Lists command types: device-specific (from its protocol) if deviceId is given, else every known type.</summary>
    [HttpGet("types")]
    public async Task<ActionResult<List<Typed>>> GetTypes([FromQuery] long deviceId = 0)
    {
        if (deviceId != 0)
        {
            var protocol = await GetDeviceProtocolAsync(deviceId);
            return protocol != null
                ? protocol.SupportedDataCommands.Select(type => new Typed(type)).ToList()
                : [new Typed(Command.TypeCustom)];
        }

        return typeof(Command)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.Name.StartsWith("Type", StringComparison.Ordinal))
            .Select(field => new Typed((string)field.GetRawConstantValue()!))
            .ToList();
    }
}
