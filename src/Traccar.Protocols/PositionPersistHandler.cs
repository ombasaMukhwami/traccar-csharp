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
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is Position position)
        {
            _ = SaveAsync(context, position);
        }
        else
        {
            context.FireChannelRead(message);
        }
    }

    private async Task SaveAsync(IChannelHandlerContext context, Position position)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.Positions.Add(position);
            await db.SaveChangesAsync();

            // Only overwrite the device's current position when this fix is not older than the
            // cached last fix — prevents archival replay from clobbering newer live positions.
            var last = positionCache.GetLastPosition(position.DeviceId);
            bool isLatest = last == null ||
                position.FixTime.GetValueOrDefault() >= last.FixTime.GetValueOrDefault();

            if (isLatest)
            {
                var device = await db.Devices.FindAsync(position.DeviceId);
                if (device != null)
                {
                    device.PositionId = position.Id;
                    device.LastUpdate = position.DeviceTime ?? position.ServerTime;
                    await db.SaveChangesAsync();
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
