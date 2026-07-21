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
    /// <summary>
    /// Deliberately synchronous — its sole caller (Jt1078ProtocolDecoder) blocks on this from a
    /// DotNetty I/O thread, and blocking on an async Task from within DotNetty's own
    /// SingleThreadEventExecutor deadlocks (the continuation is scheduled back onto the very
    /// thread that's blocked waiting for it). See ConnectionManager.GetDeviceSession for the same
    /// fix applied to the equivalent hot path.
    /// </summary>
    public Device? Lookup(params string[] uniqueIds)
    {
        using var db = dbContextFactory.CreateDbContext();
        foreach (var uniqueId in uniqueIds)
        {
            var device = db.Devices.FirstOrDefault(d => d.UniqueId == uniqueId);
            if (device != null)
            {
                return device;
            }
        }
        return null;
    }
}
