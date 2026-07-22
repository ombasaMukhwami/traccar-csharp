using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols;

public sealed class PositionPersistHandler(
    IDbContextFactory<TraccarDbContext> dbContextFactory,
    PositionCache positionCache,
    ILogger<PositionPersistHandler> logger) : ChannelHandlerAdapter
{
    /// <summary>
    /// Deliberately synchronous, and called directly rather than fire-and-forget — every caller
    /// is a DotNetty I/O thread, and blocking on an async Task there doesn't reliably resume
    /// (DotNetty's SingleThreadEventExecutor doesn't service posted continuations the way a
    /// normal SynchronizationContext does; see ConnectionManager.GetDeviceSession for the same
    /// issue on the equivalent connection-handling path). Running inline also fixes an ordering
    /// bug the previous fire-and-forget version had: multiple positions for the same device
    /// arriving in quick succession could race on updating Device.PositionId.
    /// </summary>
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is Position position)
        {
            Save(context, position);
        }
        else
        {
            context.FireChannelRead(message);
        }
    }

    private void Save(IChannelHandlerContext context, Position position)
    {
        try
        {
            using var db = dbContextFactory.CreateDbContext();
            db.Positions.Add(position);
            db.SaveChanges();

            // Only overwrite the device's current position when this fix is not older than the
            // cached last fix — prevents archival replay from clobbering newer live positions.
            var last = positionCache.GetLastPosition(position.DeviceId);
            bool isLatest = last == null ||
                position.FixTime.GetValueOrDefault() >= last.FixTime.GetValueOrDefault();

            if (isLatest)
            {
                var device = db.Devices.Find(position.DeviceId);
                if (device != null)
                {
                    device.PositionId = position.Id;
                    device.LastUpdate = position.DeviceTime ?? position.ServerTime;
                    db.SaveChanges();
                }
            }

            // Fire downstream while the cache still holds the previous position so that
            // EventProcessingHandler sees the old last for duplicate-alarm / proximity checks.
            context.FireChannelRead(position);

            if (isLatest)
                positionCache.UpdatePosition(position);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to save position for device {DeviceId}", position.DeviceId);
        }
    }
}
