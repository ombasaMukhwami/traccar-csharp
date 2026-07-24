using DotNetty.Transport.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Handlers;

/// <summary>
/// The last DotNetty pipeline stage a position passes through — everything after this point
/// (forward, persist, event-analysis) runs as plain method calls via <paramref name="continuation"/>
/// rather than further pipeline stages, specifically because of this handler's async gap.
///
/// Resolving an address is a genuine network round-trip, too slow to block a pipeline thread on,
/// so it runs via Task.Run on a thread-pool thread. That thread-pool continuation used to resume
/// processing with context.FireChannelRead(position) (optionally marshaled back onto the channel's
/// event loop via context.Executor.Execute) — but both silently drop the position if the channel
/// has since closed, which happens routinely: devices that upload a burst then disconnect, or
/// simply many positions in flight when a connection drops. Confirmed by testing: sending 1000
/// positions back-to-back and closing the socket immediately after saved zero of them, no
/// exception, no log — versus all 1000 when the socket was kept open. Calling
/// <paramref name="continuation"/> directly has no dependency on the channel at all, so it keeps
/// working regardless of what the connection that produced the position is doing by the time
/// geocoding finishes.
/// </summary>
public sealed class GeocoderHandler(
    IGeocoderService? geocoder,
    PositionCache positionCache,
    IConfiguration configuration,
    ILogger<GeocoderHandler> logger,
    Action<Position> continuation) : ChannelHandlerAdapter
{
    private readonly bool _ignorePositions = configuration.GetValue(ConfigKeys.Geocoder.IgnorePositions, false);
    private readonly double _reuseDistance = configuration.GetValue(ConfigKeys.Geocoder.ReuseDistance, 0.0);

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is not Position position)
        {
            context.FireChannelRead(message);
            return;
        }

        if (geocoder == null || _ignorePositions || position.Address != null)
        {
            continuation(position);
            return;
        }

        _ = Task.Run(() => GeocodeAsync(position));
    }

    private async Task GeocodeAsync(Position position)
    {
        try
        {
            if (_reuseDistance > 0)
            {
                var last = positionCache.GetLastPosition(position.DeviceId);
                if (last?.Address != null)
                {
                    double dist = DistanceCalculator.Distance(
                        position.Latitude, position.Longitude,
                        last.Latitude, last.Longitude);
                    if (dist <= _reuseDistance)
                    {
                        position.Address = last.Address;
                        continuation(position);
                        return;
                    }
                }
            }

            position.Address = await geocoder!.GetAddressAsync(position.Latitude, position.Longitude);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Geocoding failed for ({Lat:F5}, {Lon:F5})", position.Latitude, position.Longitude);
        }

        continuation(position);
    }
}
