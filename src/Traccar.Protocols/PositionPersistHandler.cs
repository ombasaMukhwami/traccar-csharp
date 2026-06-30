using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols;

public sealed class PositionPersistHandler(
    IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<PositionPersistHandler> logger) : ChannelHandlerAdapter
{
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is Position position)
        {
            _ = SaveAsync(position);
        }
        else
        {
            context.FireChannelRead(message);
        }
    }

    private async Task SaveAsync(Position position)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.Positions.Add(position);
            await db.SaveChangesAsync();

            var device = await db.Devices.FindAsync(position.DeviceId);
            if (device != null)
            {
                device.PositionId = position.Id;
                device.LastUpdate = position.DeviceTime ?? position.ServerTime;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to save position for device {DeviceId}", position.DeviceId);
        }
    }
}
