using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols.Session;

/// <summary>
/// Read-only device lookup by unique id, used by protocols (like JT1078) that need to resolve a
/// device without creating a tracked session. Mirrors Java's DeviceLookupService, minus its
/// repeated-failed-lookup throttling - a DB-load optimization not essential for correctness.
/// </summary>
public sealed class DeviceLookupService(IDbContextFactory<TraccarDbContext> dbContextFactory)
{
    public async Task<Device?> LookupAsync(params string[] uniqueIds)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        foreach (var uniqueId in uniqueIds)
        {
            var device = await db.Devices.FirstOrDefaultAsync(d => d.UniqueId == uniqueId);
            if (device != null)
            {
                return device;
            }
        }
        return null;
    }
}
