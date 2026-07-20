using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Starcom;

public sealed class StarcomFrameDecoder : BaseFrameDecoder
{
    // Frame format: |key=val,...|\r\n
    // Scan from position 1 (skip the opening '|') for the closing '|' followed by \r\n.
    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        int readable = buf.ReadableBytes;
        for (int i = 1; i + 2 < readable; i++)
        {
            int pos = buf.ReaderIndex + i;
            if (buf.GetByte(pos) == '|' && buf.GetByte(pos + 1) == '\r' && buf.GetByte(pos + 2) == '\n')
            {
                return buf.ReadRetainedSlice(i + 3);
            }
        }
        return null;
    }
}
