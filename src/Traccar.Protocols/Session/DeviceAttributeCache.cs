using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols.Session;

/// <summary>
/// Per-protocol in-memory cache for DeviceAttribute rows. Mirrors Java's CacheManager behaviour
/// of holding per-device attribute lists in memory, refreshing on a 60-second TTL.
/// </summary>
public sealed class DeviceAttributeCache(IDbContextFactory<TraccarDbContext> dbContextFactory)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<long, (DateTime Expiry, IReadOnlyList<DeviceAttribute> Items)> _cache = new();

    /// <summary>
    /// Deliberately synchronous — every caller runs on a DotNetty I/O thread as part of the
    /// position-processing pipeline, and awaiting an async EF call there doesn't reliably resume
    /// (DotNetty's SingleThreadEventExecutor doesn't service posted continuations the way a
    /// normal SynchronizationContext does). See ConnectionManager.GetDeviceSession for the same
    /// issue and fix on the equivalent connection-handling hot path.
    /// </summary>
    public IReadOnlyList<DeviceAttribute> Get(long deviceId)
    {
        if (_cache.TryGetValue(deviceId, out var entry) && entry.Expiry > DateTime.UtcNow)
            return entry.Items;

        using var db = dbContextFactory.CreateDbContext();
        var items = db.DeviceAttributes
            .Where(a => a.DeviceId == deviceId)
            .ToList();

        _cache[deviceId] = (DateTime.UtcNow.Add(Ttl), items);
        return items;
    }

    public void Invalidate(long deviceId) => _cache.TryRemove(deviceId, out _);
}
