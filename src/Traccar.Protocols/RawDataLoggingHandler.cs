using System.Net;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols;

/// <summary>
/// Logs every raw message in both directions, mirroring Java's StandardLoggingHandler. Printable
/// payloads are logged as text (line breaks escaped); everything else is hex-dumped.
/// </summary>
public sealed class RawDataLoggingHandler(string protocolName, ILogger logger) : ChannelHandlerAdapter
{
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        Log(context, received: true, message);
        context.FireChannelRead(message);
    }

    public override Task WriteAsync(IChannelHandlerContext context, object message)
    {
        Log(context, received: false, message);
        return context.WriteAsync(message);
    }

    private void Log(IChannelHandlerContext context, bool received, object message)
    {
        IByteBuffer buffer;
        EndPoint? remoteAddress;
        switch (message)
        {
            case DatagramPacket packet:
                buffer = packet.Content;
                remoteAddress = received ? packet.Sender : packet.Recipient;
                break;
            case IByteBuffer buf:
                buffer = buf;
                remoteAddress = context.Channel.RemoteAddress;
                break;
            default:
                return;
        }

        var wrapped = new ByteBuf(buffer);
        var data = BufferUtil.IsPrintable(wrapped, buffer.ReadableBytes)
            ? buffer.ToString(buffer.ReaderIndex, buffer.ReadableBytes, Encoding.ASCII).Replace("\r", "\\r").Replace("\n", "\\n")
            : ByteBufferUtil.HexDump(buffer);

        logger.LogInformation(
            "[{ChannelId}: {Protocol} {Direction} {RemoteAddress}] {Data}",
            context.Channel.Id.AsShortText(), protocolName, received ? "<" : ">", remoteAddress, data);
    }
}
