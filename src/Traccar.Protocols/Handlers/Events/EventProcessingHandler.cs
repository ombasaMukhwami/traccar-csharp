using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols.Handlers.Events;

public sealed class EventProcessingHandler(
    IReadOnlyList<BaseEventHandler> eventHandlers,
    PositionCache positionCache,
    IDbContextFactory<TraccarDbContext> dbContextFactory,
    ILogger<EventProcessingHandler> logger) : ChannelHandlerAdapter
{
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is Position position)
        {
            // PositionPersistHandler fires downstream before updating the cache, so GetLastPosition
            // here still returns the previous position — exactly what event handlers need.
            var last = positionCache.GetLastPosition(position.DeviceId);
            _ = RunHandlersAsync(position, last);
        }
        // Terminal handler — no FireChannelRead.
    }

    private async Task RunHandlersAsync(Position position, Position? last)
    {
        var events = new List<Event>();
        foreach (var handler in eventHandlers)
            await handler.AnalyzeAsync(position, last, events.Add);

        if (events.Count == 0)
            return;

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.Events.AddRange(events);
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to save {Count} events for device {DeviceId}",
                events.Count, position.DeviceId);
        }
    }
}
