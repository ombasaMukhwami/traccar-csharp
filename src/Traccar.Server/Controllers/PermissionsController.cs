using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Server.Controllers;

/// <summary>
/// Mirrors Java's PermissionsResource. Manages many-to-many links between entities
/// (user↔device, user↔group, group↔device). The request body is a flat dictionary with
/// exactly two "Id"-suffixed keys that identify the entity pair, e.g. {"userId":1,"deviceId":5}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/permissions")]
public class PermissionsController(TraccarDbContext db) : ControllerBase
{
    // -------------------------------------------------------------------------
    // Link-type resolution
    // -------------------------------------------------------------------------

    private enum LinkType { UserDevice, UserGroup, GroupDevice }

    private static bool TryGetLinkType(Dictionary<string, long> entity, out LinkType type, out string? error)
    {
        bool hasUser = entity.ContainsKey("userId");
        bool hasDevice = entity.ContainsKey("deviceId");
        bool hasGroup = entity.ContainsKey("groupId");

        if (hasUser && hasDevice) { type = LinkType.UserDevice; error = null; return true; }
        if (hasUser && hasGroup)  { type = LinkType.UserGroup;  error = null; return true; }
        if (hasGroup && hasDevice){ type = LinkType.GroupDevice; error = null; return true; }

        type = default;
        error = "Body must contain exactly two Id keys: userId+deviceId, userId+groupId, or groupId+deviceId.";
        return false;
    }

    // -------------------------------------------------------------------------
    // GET — list existing links; pass 0 for either side to match all
    // -------------------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<List<Dictionary<string, long>>>> Get()
    {
        var query = HttpContext.Request.Query;
        var ids = query.Where(kv => kv.Key.EndsWith("Id", StringComparison.Ordinal))
                       .ToDictionary(kv => kv.Key, kv => long.Parse(kv.Value.ToString()));

        if (!TryGetLinkType(ids, out var linkType, out var err))
        {
            return BadRequest(err);
        }

        long userId = ids.GetValueOrDefault("userId");
        long groupId = ids.GetValueOrDefault("groupId");
        long deviceId = ids.GetValueOrDefault("deviceId");

        List<Dictionary<string, long>> result = linkType switch
        {
            LinkType.UserDevice => (await db.UserDevices
                .Where(x => (userId == 0 || x.UserId == userId) && (deviceId == 0 || x.DeviceId == deviceId))
                .Select(x => new { x.UserId, x.DeviceId })
                .ToListAsync())
                .Select(x => new Dictionary<string, long> { ["userId"] = x.UserId, ["deviceId"] = x.DeviceId })
                .ToList(),

            LinkType.UserGroup => (await db.UserGroups
                .Where(x => (userId == 0 || x.UserId == userId) && (groupId == 0 || x.GroupId == groupId))
                .Select(x => new { x.UserId, x.GroupId })
                .ToListAsync())
                .Select(x => new Dictionary<string, long> { ["userId"] = x.UserId, ["groupId"] = x.GroupId })
                .ToList(),

            LinkType.GroupDevice => (await db.GroupDevices
                .Where(x => (groupId == 0 || x.GroupId == groupId) && (deviceId == 0 || x.DeviceId == deviceId))
                .Select(x => new { x.GroupId, x.DeviceId })
                .ToListAsync())
                .Select(x => new Dictionary<string, long> { ["groupId"] = x.GroupId, ["deviceId"] = x.DeviceId })
                .ToList(),

            _ => [],
        };

        return result;
    }

    // -------------------------------------------------------------------------
    // POST — add a single link
    // -------------------------------------------------------------------------

    [HttpPost]
    public Task<IActionResult> Add([FromBody] Dictionary<string, long> entity) =>
        AddBulk([entity]);

    [HttpPost("bulk")]
    public async Task<IActionResult> AddBulk([FromBody] List<Dictionary<string, long>> entities)
    {
        if (!ValidateConsistentKeys(entities, out var err))
        {
            return BadRequest(err);
        }

        foreach (var entity in entities)
        {
            if (!TryGetLinkType(entity, out var linkType, out var typeErr))
            {
                return BadRequest(typeErr);
            }

            switch (linkType)
            {
                case LinkType.UserDevice:
                    var ud = new UserDevice { UserId = entity["userId"], DeviceId = entity["deviceId"] };
                    if (!await db.UserDevices.AnyAsync(x => x.UserId == ud.UserId && x.DeviceId == ud.DeviceId))
                    {
                        db.UserDevices.Add(ud);
                    }
                    break;

                case LinkType.UserGroup:
                    var ug = new UserGroup { UserId = entity["userId"], GroupId = entity["groupId"] };
                    if (!await db.UserGroups.AnyAsync(x => x.UserId == ug.UserId && x.GroupId == ug.GroupId))
                    {
                        db.UserGroups.Add(ug);
                    }
                    break;

                case LinkType.GroupDevice:
                    var gd = new GroupDevice { GroupId = entity["groupId"], DeviceId = entity["deviceId"] };
                    if (!await db.GroupDevices.AnyAsync(x => x.GroupId == gd.GroupId && x.DeviceId == gd.DeviceId))
                    {
                        db.GroupDevices.Add(gd);
                    }
                    break;
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // DELETE — remove a single link
    // -------------------------------------------------------------------------

    [HttpDelete]
    public Task<IActionResult> Remove([FromBody] Dictionary<string, long> entity) =>
        RemoveBulk([entity]);

    [HttpDelete("bulk")]
    public async Task<IActionResult> RemoveBulk([FromBody] List<Dictionary<string, long>> entities)
    {
        if (!ValidateConsistentKeys(entities, out var err))
        {
            return BadRequest(err);
        }

        foreach (var entity in entities)
        {
            if (!TryGetLinkType(entity, out var linkType, out var typeErr))
            {
                return BadRequest(typeErr);
            }

            switch (linkType)
            {
                case LinkType.UserDevice:
                    var ud = await db.UserDevices.FindAsync(entity["userId"], entity["deviceId"]);
                    if (ud != null) db.UserDevices.Remove(ud);
                    break;

                case LinkType.UserGroup:
                    var ug = await db.UserGroups.FindAsync(entity["userId"], entity["groupId"]);
                    if (ug != null) db.UserGroups.Remove(ug);
                    break;

                case LinkType.GroupDevice:
                    var gd = await db.GroupDevices.FindAsync(entity["groupId"], entity["deviceId"]);
                    if (gd != null) db.GroupDevices.Remove(gd);
                    break;
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------------------------------------------------------------

    private static bool ValidateConsistentKeys(List<Dictionary<string, long>> entities, out string? error)
    {
        if (entities.Count == 0) { error = null; return true; }
        var referenceKeys = entities[0].Keys.ToHashSet();
        foreach (var entity in entities.Skip(1))
        {
            if (!entity.Keys.ToHashSet().SetEquals(referenceKeys))
            {
                error = "All entities in a bulk operation must have the same key types.";
                return false;
            }
        }
        error = null;
        return true;
    }
}
