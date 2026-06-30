using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Gt06;

public sealed class Gt06FrameDecoder() : BaseFrameDecoder(LargeMaxFrameLength)
{
    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        if (buf.ReadableBytes < 5)
        {
            return null;
        }

        var length = 2 + 2; // head and tail

        if (buf.GetByte(buf.ReaderIndex) == 0x78)
        {
            length += 1 + buf.GetUnsignedByte(buf.ReaderIndex + 2);
        }
        else
        {
            length += 2 + buf.GetUnsignedShort(buf.ReaderIndex + 2);
        }

        if (buf.ReadableBytes >= length && buf.GetUnsignedShort(buf.ReaderIndex + length - 2) == 0x0d0a)
        {
            return buf.ReadRetainedSlice(length);
        }

        var endIndex = buf.ReaderIndex - 1;
        do
        {
            endIndex = buf.IndexOf(endIndex + 1, buf.WriterIndex, 0x0d);
            if (endIndex > 0 && buf.WriterIndex > endIndex + 1 && buf.GetByte(endIndex + 1) == 0x0a)
            {
                return buf.ReadRetainedSlice(endIndex + 2 - buf.ReaderIndex);
            }
        } while (endIndex > 0);

        return null;
    }
}
