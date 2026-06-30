using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;

namespace Traccar.Protocols;

/// <summary>
/// Closes the connection on read-idle timeout, mirroring Java's MainEventHandler reacting to
/// IdleStateEvent. Paired with an IdleStateHandler added earlier in the pipeline.
/// </summary>
public sealed class IdleDisconnectHandler : ChannelHandlerAdapter
{
    public override void UserEventTriggered(IChannelHandlerContext context, object evt)
    {
        if (evt is IdleStateEvent)
        {
            context.CloseAsync();
        }
        else
        {
            context.FireUserEventTriggered(evt);
        }
    }
}
