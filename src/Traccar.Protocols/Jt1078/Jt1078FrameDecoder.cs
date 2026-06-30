using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Jt1078;

public sealed class Jt1078FrameDecoder() : BaseFrameDecoder(LargeMaxFrameLength)
{
    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        if (buf.ReadableBytes < 34)
        {
            return null;
        }

        var startIndex = buf.ReaderIndex;
        var idLength = buf.GetUnsignedShort(startIndex + 8) == 0 ? 10 : 6;
        var index = startIndex + 9 + idLength; // skip header

        var type = BitUtil.From(buf.GetUnsignedByte(index), 4);
        index += 1 + 8; // data type + timestamp

        if (type <= 2)
        {
            index += 4; // i-frame interval + frame interval
        }

        if (buf.ReadableBytes < index - startIndex + 2)
        {
            return null;
        }

        var bodyLength = buf.GetUnsignedShort(index);
        index += 2;

        var length = index - startIndex + bodyLength;
        if (buf.ReadableBytes < length)
        {
            return null;
        }

        return buf.ReadRetainedSlice(length);
    }
}
