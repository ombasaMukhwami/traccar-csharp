using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

namespace Traccar.Protocols;

/// <summary>
/// Converts outbound string messages to ASCII-encoded bytes, mirroring Netty's StringEncoder.
/// Required ahead of any StringProtocolEncoder in the pipeline (e.g. H02, GL200), since those
/// encoders produce plain strings that DotNetty cannot write to a socket on their own.
/// </summary>
public sealed class StringEncoderHandler : ChannelHandlerAdapter
{
    public override Task WriteAsync(IChannelHandlerContext context, object message)
        => message is string text ? context.WriteAsync(Unpooled.CopiedBuffer(text, Encoding.ASCII)) : context.WriteAsync(message);
}
