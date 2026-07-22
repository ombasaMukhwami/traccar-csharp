using Microsoft.Extensions.Logging;
using Traccar.Model;

namespace Traccar.Protocols.Handlers.Events;

public abstract class BaseEventHandler(ILogger logger)
{
    /// <summary>Deliberately synchronous — every caller runs on a DotNetty I/O thread as part of
    /// the position-processing pipeline. See ConnectionManager.GetDeviceSession for why awaiting
    /// an async EF call there doesn't reliably resume.</summary>
    public void Analyze(Position position, Position? last, Action<Event> callback)
    {
        try
        {
            OnPosition(position, last, callback);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Event handler failed");
        }
    }

    protected abstract void OnPosition(Position position, Position? last, Action<Event> callback);
}
