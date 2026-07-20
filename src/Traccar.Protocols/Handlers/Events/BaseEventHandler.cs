using Microsoft.Extensions.Logging;
using Traccar.Model;

namespace Traccar.Protocols.Handlers.Events;

public abstract class BaseEventHandler(ILogger logger)
{
    public async ValueTask AnalyzeAsync(Position position, Position? last, Action<Event> callback)
    {
        try
        {
            await OnPositionAsync(position, last, callback);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Event handler failed");
        }
    }

    protected abstract ValueTask OnPositionAsync(Position position, Position? last, Action<Event> callback);
}
