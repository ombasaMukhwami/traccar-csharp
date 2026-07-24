using System.Collections.Concurrent;
using System.Net;
using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols.Session;

public sealed class ConnectionManager(
    IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<ConnectionManager> logger)
{
    private readonly ConcurrentDictionary<long, DeviceSession> _sessionsByDeviceId = new();
    private readonly ConcurrentDictionary<IChannel, ConcurrentDictionary<string, DeviceSession>> _sessionsByChannel = new();

    public DeviceSession? GetDeviceSession(long deviceId)
        => _sessionsByDeviceId.GetValueOrDefault(deviceId);

    /// <summary>
    /// Resolves (or auto-registers) the device session for a connecting device. Deliberately
    /// synchronous rather than async: every caller is a DotNetty I/O-thread handler that can only
    /// consume this result by blocking on it anyway (DotNetty's ChannelRead is synchronous), and
    /// blocking on an async Task from within DotNetty's own SingleThreadEventExecutor deadlocks —
    /// the continuation gets scheduled back onto the very thread that's blocked waiting for it.
    /// Plain synchronous EF calls avoid that entirely.
    /// </summary>
    public DeviceSession? GetDeviceSession(IChannel channel, EndPoint? remoteAddress, params string?[] uniqueIds)
    {
        var ids = uniqueIds.Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToArray();
        _sessionsByChannel.TryGetValue(channel, out var channelSessions);

        if (ids.Length > 0)
        {
            if (channelSessions != null)
            {
                foreach (var id in ids)
                {
                    if (channelSessions.TryGetValue(id, out var existing))
                    {
                        existing.LastUpdate = DateTime.UtcNow;
                        return existing;
                    }
                }
            }
        }
        else
        {
            var any = channelSessions?.Values.FirstOrDefault();
            if (any != null)
            {
                any.LastUpdate = DateTime.UtcNow;
            }
            return any;
        }

        using var db = dbContextFactory.CreateDbContext();
        var device = db.Devices.FirstOrDefault(d => ids.Contains(d.UniqueId));

        if (device == null)
        {
            // Unknown devices are still auto-registered on first contact (matching original
            // Traccar) rather than rejected — but every device now requires a real ClientId (see
            // TraccarDbContext's FK on Device.ClientId), so this assigns the table-wide default
            // client (Client.IsDefault — see AdministrativeClientsController.MarkDefault) instead
            // of leaving it unset. If no default client exists yet, there's truly nothing valid
            // to assign, so the connection is rejected rather than violating the FK.
            var defaultClientId = db.Clients.Where(c => c.IsDefault).Select(c => (int?)c.Id).FirstOrDefault();
            if (defaultClientId == null)
            {
                logger.LogWarning("Rejected connection from unknown device {UniqueId} at {RemoteAddress} — " +
                    "no default client configured to auto-assign it to.", ids[0], remoteAddress);
                return null;
            }

            device = new Device { Name = ids[0], UniqueId = ids[0], ClientId = defaultClientId.Value };
            db.Devices.Add(device);
            db.SaveChanges();
            logger.LogInformation("Automatically registered {UniqueId} under default client {ClientId}", ids[0], defaultClientId);
        }

        if (_sessionsByDeviceId.TryRemove(device.Id, out var oldSession)
            && _sessionsByChannel.TryGetValue(oldSession.Channel, out var oldChannelSessions))
        {
            oldChannelSessions.TryRemove(oldSession.UniqueId, out _);
            if (oldChannelSessions.IsEmpty)
            {
                _sessionsByChannel.TryRemove(oldSession.Channel, out _);
            }
        }

        var session = new DeviceSession(device.Id, device.UniqueId, device.Model, channel, remoteAddress);
        var newChannelSessions = _sessionsByChannel.GetOrAdd(channel, _ => new ConcurrentDictionary<string, DeviceSession>());
        newChannelSessions[device.UniqueId] = session;
        _sessionsByDeviceId[device.Id] = session;

        logger.LogInformation(
            "New connection from {UniqueId} at {RemoteAddress} [{ChannelId}]",
            device.UniqueId, remoteAddress, channel.Id.AsShortText());

        return session;
    }

    /// <summary>
    /// Deliberately synchronous, for the same reason as GetDeviceSession above. Callers that want
    /// this off the calling DotNetty thread (it doesn't need to block a channel event) should
    /// wrap the call in Task.Run, which hands it to a genuine thread-pool thread — safe for a
    /// blocking call, unlike relying on an awaited Task to resume on DotNetty's own executor.
    /// </summary>
    public void UpdateDeviceStatus(long deviceId, string status, DateTime? time)
    {
        try
        {
            using var db = dbContextFactory.CreateDbContext();
            var device = db.Devices.Find(deviceId);
            if (device == null)
            {
                return;
            }

            var oldStatus = device.Status;
            device.Status = status;
            if (time.HasValue)
            {
                device.LastUpdate = time;
            }

            if (oldStatus != status)
            {
                var eventType = status switch
                {
                    Device.StatusOnline => Event.TypeDeviceOnline,
                    Device.StatusUnknown => Event.TypeDeviceUnknown,
                    _ => Event.TypeDeviceOffline,
                };
                db.Events.Add(new Event(eventType, deviceId));
            }

            db.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Update device status error");
        }
    }

    public void DeviceDisconnected(IChannel channel)
    {
        if (!_sessionsByChannel.TryRemove(channel, out var channelSessions))
        {
            return;
        }
        foreach (var session in channelSessions.Values)
        {
            _sessionsByDeviceId.TryRemove(session.DeviceId, out _);
            var deviceId = session.DeviceId;
            _ = Task.Run(() => UpdateDeviceStatus(deviceId, Device.StatusOffline, null));
        }
    }
}
