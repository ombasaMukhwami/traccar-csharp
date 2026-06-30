using System.Collections.Concurrent;
using DotNetty.Transport.Channels;

namespace Traccar.Protocols;

/// <summary>
/// Tracks active connections for a ProtocolServer so they can all be force-closed on shutdown,
/// mirroring Java's TrackerServer.channelGroup / OpenChannelHandler.
/// </summary>
public sealed class OpenChannelHandler(ConcurrentDictionary<IChannel, byte> channels) : ChannelHandlerAdapter
{
    public override void ChannelActive(IChannelHandlerContext context)
    {
        channels[context.Channel] = 0;
        context.FireChannelActive();
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        channels.TryRemove(context.Channel, out _);
        context.FireChannelInactive();
    }
}
