using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Session;

namespace Traccar.Protocols;

/// <summary>
/// Mirrors Java's MainEventHandler: logs connect/disconnect/error events and notifies the
/// ConnectionManager when a channel closes so device status can be updated to offline.
/// </summary>
public sealed class ConnectionTrackingHandler(ConnectionManager connectionManager, ILogger logger) : ChannelHandlerAdapter
{
    public override void ChannelActive(IChannelHandlerContext context)
    {
        logger.LogInformation(
            "Connected [{ChannelId}] from {RemoteAddress}", context.Channel.Id.AsShortText(), context.Channel.RemoteAddress);
        context.FireChannelActive();
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        logger.LogInformation("Disconnected [{ChannelId}]", context.Channel.Id.AsShortText());
        connectionManager.DeviceDisconnected(context.Channel);
        context.FireChannelInactive();
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
        // Unwrap to root cause (mirrors Java MainEventHandler.exceptionCaught chain walk).
        var cause = exception;
        while (cause.InnerException != null && cause.InnerException != cause)
            cause = cause.InnerException;

        logger.LogWarning(cause, "Error [{ChannelId}]", context.Channel.Id.AsShortText());
        context.CloseAsync();
    }
}
