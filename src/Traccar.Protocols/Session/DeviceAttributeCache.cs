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

    public async ValueTask<IReadOnlyList<DeviceAttribute>> GetAsync(long deviceId)
    {
        if (_cache.TryGetValue(deviceId, out var entry) && entry.Expiry > DateTime.UtcNow)
            return entry.Items;

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var items = await db.DeviceAttributes
            .Where(a => a.DeviceId == deviceId)
            .ToListAsync();

        _cache[deviceId] = (DateTime.UtcNow.Add(Ttl), items);
        return items;
    }

    public void Invalidate(long deviceId) => _cache.TryRemove(deviceId, out _);
}
