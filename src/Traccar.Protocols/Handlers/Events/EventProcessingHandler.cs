using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols.Handlers.Events;

/// <summary>
/// A plain class rather than a DotNetty pipeline handler — see BaseProtocol.AddPositionServer's
/// ContinuePosition, which calls <see cref="Process"/> directly, independent of the producing
/// channel's lifecycle (see GeocoderHandler's doc comment for why that matters).
/// </summary>
public sealed class EventProcessingHandler(
    IReadOnlyList<BaseEventHandler> eventHandlers,
    PositionCache positionCache,
    IDbContextFactory<TraccarDbContext> dbContextFactory,
    ILogger<EventProcessingHandler> logger)
{
    public void Process(Position position)
    {
        // PositionPersistHandler calls onSaved before updating the cache, so GetLastPosition here
        // still returns the previous position — exactly what event handlers need.
        var last = positionCache.GetLastPosition(position.DeviceId);
        RunHandlers(position, last);
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
