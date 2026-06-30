using System.Text;
using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Meitrack;

public sealed class MeitrackFrameDecoder() : BaseFrameDecoder(LargeMaxFrameLength)
{
    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        if (buf.ReadableBytes < 10)
        {
            return null;
        }

        var index = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, (byte)',');
        if (index != -1)
        {
            var length = index - buf.ReaderIndex + int.Parse(
                buf.ToString(buf.ReaderIndex + 3, index - buf.ReaderIndex - 3, Encoding.ASCII));
            if (buf.ReadableBytes >= length)
            {
                return buf.ReadRetainedSlice(length);
            }
        }

        return null;
    }
}
