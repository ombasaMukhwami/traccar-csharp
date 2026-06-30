using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

namespace Traccar.Protocols.Jt808;

/// <summary>
/// Escapes JT808 frame delimiter bytes in outbound messages before they reach the socket, mirroring
/// Java's Jt808FrameEncoder (a MessageToByteEncoder&lt;ByteBuf&gt;). DotNetty's raw IByteBuffer.ReadByte
/// already returns an unsigned-range C# byte, matching Java's readUnsignedByte() here directly - no
/// signed conversion needed.
/// </summary>
public sealed class Jt808FrameEncoder : ChannelHandlerAdapter
{
    public override Task WriteAsync(IChannelHandlerContext context, object message)
    {
        if (message is not IByteBuffer msg)
        {
            return context.WriteAsync(message);
        }

        try
        {
            var alternative = msg.GetByte(msg.ReaderIndex) == 0xe7;

            var output = Unpooled.Buffer();
            var startIndex = msg.ReaderIndex;
            while (msg.IsReadable())
            {
                var index = msg.ReaderIndex;
                int b = msg.ReadByte();
                if (alternative && (b == 0xe6 || b == 0x3d || b == 0x3e))
                {
                    output.WriteByte(b == 0xe6 ? 0xe6 : 0x3e);
                    output.WriteByte(b == 0x3d ? 0x02 : 0x01);
                }
                else if (alternative && b == 0xe7 && index != startIndex && msg.IsReadable())
                {
                    output.WriteByte(0xe6);
                    output.WriteByte(0x02);
                }
                else if (!alternative && b == 0x7d)
                {
                    output.WriteByte(0x7d);
                    output.WriteByte(0x01);
                }
                else if (!alternative && b == 0x7e && index != startIndex && msg.IsReadable())
                {
                    output.WriteByte(0x7d);
                    output.WriteByte(0x02);
                }
                else
                {
                    output.WriteByte(b);
                }
            }

            return context.WriteAsync(output);
        }
        finally
        {
            ReferenceCountUtil.Release(msg);
        }
    }
}
