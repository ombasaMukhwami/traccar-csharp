using System.Collections.Concurrent;

namespace Traccar.Protocols.Helpers;

/// <summary>
/// Thread-safe dictionary that holds values via weak references so the GC can reclaim them
/// when no other strong references exist. Mirrors Java's helper.ConcurrentWeakValueMap.
/// </summary>
public sealed class ConcurrentWeakValueMap<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly ConcurrentDictionary<TKey, WeakReference<TValue>> _dict = new();

    public TValue? Get(TKey key) =>
        _dict.TryGetValue(key, out var weak) && weak.TryGetTarget(out var value) ? value : null;

    public void Put(TKey key, TValue value) => _dict[key] = new WeakReference<TValue>(value);

    public TValue? Remove(TKey key) =>
        _dict.TryRemove(key, out var weak) && weak.TryGetTarget(out var value) ? value : null;
}
