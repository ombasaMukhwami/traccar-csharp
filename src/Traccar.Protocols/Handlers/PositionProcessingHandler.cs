using DotNetty.Transport.Channels;
using Traccar.Model;

namespace Traccar.Protocols.Handlers;

public sealed class PositionProcessingHandler(
    PositionCache positionCache,
    OutdatedHandler outdatedHandler,
    ComputedAttributesHandler computedEarlyHandler,
    CopyAttributesHandler copyAttributesHandler,
    DistanceHandler distanceHandler,
    EngineHoursHandler engineHoursHandler,
    MotionHandler motionHandler,
    ComputedAttributesHandler computedLateHandler,
    FilterHandler filterHandler,
    PositionAttributesHandler attributesHandler) : ChannelHandlerAdapter
{
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (message is not Position position)
        {
            context.FireChannelRead(message);
            return;
        }

        var last = positionCache.GetLastPosition(position.DeviceId);
        outdatedHandler.Process(position, last);
        computedEarlyHandler.Process(position, last);
        copyAttributesHandler.Process(position, last);
        distanceHandler.Process(position, last);
        engineHoursHandler.Process(position, last);
        motionHandler.Process(position);
        computedLateHandler.Process(position, last);
        if (filterHandler.Filter(position, last)) return;
        attributesHandler.Extract(position);
        context.FireChannelRead(position);
    }
}
