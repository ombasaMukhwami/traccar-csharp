using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Session;

namespace Traccar.Protocols;

/// <summary>
/// Logs every accepted TCP connection as soon as it's established, mirroring Java's
/// MainEventHandler.channelActive ("[session] connected"). At this point the device hasn't sent
/// anything yet, so the remote address is all that's known; ConnectionManager separately logs the
/// device's unique id once it identifies itself.
/// </summary>
public sealed class ConnectionTrackingHandler(ConnectionManager connectionManager, ILogger logger) : ChannelHandlerAdapter
{
    public override void ChannelActive(IChannelHandlerContext context)
    {
        logger.LogInformation(
            "New connection from {RemoteAddress} [{ChannelId}]", context.Channel.RemoteAddress, context.Channel.Id.AsShortText());
        context.FireChannelActive();
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        connectionManager.DeviceDisconnected(context.Channel);
        context.FireChannelInactive();
    }
}
