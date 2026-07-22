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
    /// <summary>Deliberately synchronous and called inline (not fire-and-forget) — every caller
    /// is a DotNetty I/O thread. See ConnectionManager.GetDeviceSession for why awaiting an async
    /// EF call there doesn't reliably resume.</summary>
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is Position position)
        {
            // PositionPersistHandler fires downstream before updating the cache, so GetLastPosition
            // here still returns the previous position — exactly what event handlers need.
            var last = positionCache.GetLastPosition(position.DeviceId);
            RunHandlers(position, last);
        }
        // Terminal handler — no FireChannelRead.
    }

    private void RunHandlers(Position position, Position? last)
    {
        var events = new List<Event>();
        foreach (var handler in eventHandlers)
            handler.Analyze(position, last, events.Add);

        if (events.Count == 0)
            return;

        try
        {
            using var db = dbContextFactory.CreateDbContext();
            db.Events.AddRange(events);
            db.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to save {Count} events for device {DeviceId}",
                events.Count, position.DeviceId);
        }
    }
}
