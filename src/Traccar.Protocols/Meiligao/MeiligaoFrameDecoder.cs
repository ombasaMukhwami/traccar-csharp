using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Meiligao;

public sealed class MeiligaoFrameDecoder() : BaseFrameDecoder(LargeMaxFrameLength)
{
    private const int MessageHeader = 4;

    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        // Strip not '$' (0x24) bytes from the beginning
        while (buf.IsReadable() && buf.GetUnsignedByte(buf.ReaderIndex) != 0x24)
        {
            buf.ReadByte();
        }

        // Check length and return buffer
        if (buf.ReadableBytes >= MessageHeader)
        {
            var length = buf.GetUnsignedShort(buf.ReaderIndex + 2);
            if (buf.ReadableBytes >= length)
            {
                return buf.ReadRetainedSlice(length);
            }
        }

        return null;
    }
}
