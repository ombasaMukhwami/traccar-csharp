using System.Collections.Concurrent;
using System.Net;
using DotNetty.Transport.Channels;
using Traccar.Model;

namespace Traccar.Protocols.Session;

public sealed class DeviceSession(
    long deviceId, string uniqueId, string? model, IChannel channel, EndPoint? remoteAddress)
{
    public const string KeyTimezone = "timezone";

    public long DeviceId { get; } = deviceId;

    public string UniqueId { get; } = uniqueId;

    public string? Model { get; } = model;

    public IChannel Channel { get; } = channel;

    public EndPoint? RemoteAddress { get; } = remoteAddress;

    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

    private readonly ConcurrentDictionary<string, object> _locals = new();

    public bool Contains(string key) => _locals.ContainsKey(key);

    public void Set(string key, object? value)
    {
        if (value is null)
        {
            _locals.TryRemove(key, out _);
        }
        else
        {
            _locals[key] = value;
        }
    }

    public T? Get<T>(string key) => _locals.TryGetValue(key, out var value) ? (T)value : default;

    /// <summary>
    /// Writes a command through this device's channel; the protocol's encoder (already in the
    /// pipeline) intercepts and serializes it. Mirrors Java's DeviceSession.sendCommand.
    /// </summary>
    public void SendCommand(Command command) => Channel.WriteAndFlushAsync(command);
}
