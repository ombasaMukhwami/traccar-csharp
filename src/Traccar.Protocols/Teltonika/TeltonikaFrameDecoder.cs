using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Teltonika;

public sealed class TeltonikaFrameDecoder() : BaseFrameDecoder(LargeMaxFrameLength)
{
    private const int MessageMinimumLength = 12;

    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        if (buf.IsReadable() && buf.GetByte(buf.ReaderIndex) == unchecked((sbyte)0xff))
        {
            return buf.ReadRetainedSlice(1);
        }

        if (buf.ReadableBytes < MessageMinimumLength)
        {
            return null;
        }

        var length = buf.GetUnsignedShort(buf.ReaderIndex);
        if (length > 0)
        {
            if (buf.ReadableBytes >= length + 2)
            {
                return buf.ReadRetainedSlice(length + 2);
            }
        }
        else
        {
            var dataLength = buf.GetInt(buf.ReaderIndex + 4);
            if (buf.ReadableBytes >= dataLength + 12)
            {
                return buf.ReadRetainedSlice(dataLength + 12);
            }
        }

        return null;
    }
}
