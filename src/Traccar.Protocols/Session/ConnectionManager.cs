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
    private readonly ConcurrentDictionary<long, DeviceSession> sessionsByDeviceId = new();
    private readonly ConcurrentDictionary<IChannel, ConcurrentDictionary<string, DeviceSession>> sessionsByChannel = new();

    public DeviceSession? GetDeviceSession(long deviceId)
        => sessionsByDeviceId.GetValueOrDefault(deviceId);

    public async Task<DeviceSession?> GetDeviceSessionAsync(
        IChannel channel, EndPoint? remoteAddress, params string?[] uniqueIds)
    {
        var ids = uniqueIds.Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToArray();
        sessionsByChannel.TryGetValue(channel, out var channelSessions);

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

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var device = await db.Devices.FirstOrDefaultAsync(d => ids.Contains(d.UniqueId));

        if (device == null)
        {
            // No device-provisioning UI/API exists yet in this build, so unknown devices are
            // auto-registered on first contact rather than silently dropped.
            device = new Device { Name = ids[0], UniqueId = ids[0] };
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            logger.LogInformation("Automatically registered {UniqueId}", ids[0]);
        }

        if (sessionsByDeviceId.TryRemove(device.Id, out var oldSession)
            && sessionsByChannel.TryGetValue(oldSession.Channel, out var oldChannelSessions))
        {
            oldChannelSessions.TryRemove(oldSession.UniqueId, out _);
            if (oldChannelSessions.IsEmpty)
            {
                sessionsByChannel.TryRemove(oldSession.Channel, out _);
            }
        }

        var session = new DeviceSession(device.Id, device.UniqueId, device.Model, channel, remoteAddress);
        var newChannelSessions = sessionsByChannel.GetOrAdd(channel, _ => new ConcurrentDictionary<string, DeviceSession>());
        newChannelSessions[device.UniqueId] = session;
        sessionsByDeviceId[device.Id] = session;

        logger.LogInformation(
            "New connection from {UniqueId} at {RemoteAddress} [{ChannelId}]",
            device.UniqueId, remoteAddress, channel.Id.AsShortText());

        return session;
    }

    public async Task UpdateDeviceStatusAsync(long deviceId, string status, DateTime? time)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var device = await db.Devices.FindAsync(deviceId);
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

            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Update device status error");
        }
    }

    public void DeviceDisconnected(IChannel channel)
    {
        if (!sessionsByChannel.TryRemove(channel, out var channelSessions))
        {
            return;
        }
        foreach (var session in channelSessions.Values)
        {
            sessionsByDeviceId.TryRemove(session.DeviceId, out _);
            _ = UpdateDeviceStatusAsync(session.DeviceId, Device.StatusOffline, null);
        }
    }
}
