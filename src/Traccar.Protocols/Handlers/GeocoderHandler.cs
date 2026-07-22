using DotNetty.Transport.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Handlers;

public sealed class GeocoderHandler(
    IGeocoderService geocoder,
    PositionCache positionCache,
    IConfiguration configuration,
    ILogger<GeocoderHandler> logger) : ChannelHandlerAdapter
{
    private readonly bool _ignorePositions = configuration.GetValue(ConfigKeys.Geocoder.IgnorePositions, false);
    private readonly double _reuseDistance = configuration.GetValue(ConfigKeys.Geocoder.ReuseDistance, 0.0);

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is Position position && !_ignorePositions && position.Address == null)
        {
            // Task.Run moves the whole async chain (including the HTTP await) onto a genuine
            // thread-pool thread. Calling GeocodeAsync directly here would start it on this
            // DotNetty I/O thread, and DotNetty's SingleThreadEventExecutor doesn't reliably
            // resume a continuation posted back to it — see ConnectionManager.GetDeviceSession
            // for the same issue on the (synchronous, DB-only) connection-handling path. This one
            // stays genuinely async rather than becoming blocking-synchronous, since an HTTP
            // round-trip is too slow to block a pipeline thread on.
            _ = Task.Run(() => GeocodeAsync(context, position));
            return;
        }
        context.FireChannelRead(message);
    }

    private async Task GeocodeAsync(IChannelHandlerContext context, Position position)
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
                        context.FireChannelRead(position);
                        return;
                    }
                }
            }

            position.Address = await geocoder.GetAddressAsync(position.Latitude, position.Longitude);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Geocoding failed for ({Lat:F5}, {Lon:F5})", position.Latitude, position.Longitude);
        }

        context.FireChannelRead(position);
    }
}
