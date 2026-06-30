using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.H02;

public sealed class H02FrameDecoder(int messageLength) : BaseFrameDecoder
{
    private const int MessageShort = 32;
    private const int MessageLong = 45;

    private int messageLength = messageLength;

    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        var marker = (char)buf.GetByte(buf.ReaderIndex);

        while (marker != '*' && marker != '$' && marker != 'X' && buf.ReadableBytes > 0)
        {
            buf.SkipBytes(1);
            if (buf.ReadableBytes > 0)
            {
                marker = (char)buf.GetByte(buf.ReaderIndex);
            }
        }

        switch (marker)
        {
            case '*':
                var index = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, (byte)'#');
                if (index != -1)
                {
                    var result = buf.ReadRetainedSlice(index + 1 - buf.ReaderIndex);
                    while (buf.IsReadable()
                           && (buf.GetByte(buf.ReaderIndex) == '\r' || buf.GetByte(buf.ReaderIndex) == '\n'))
                    {
                        buf.ReadByte(); // skip new line
                    }
                    return result;
                }
                break;

            case '$':
                if (messageLength == 0)
                {
                    messageLength = buf.ReadableBytes == MessageLong ? MessageLong : MessageShort;
                }
                if (buf.ReadableBytes >= messageLength)
                {
                    return buf.ReadRetainedSlice(messageLength);
                }
                break;

            case 'X':
                if (buf.ReadableBytes >= MessageShort)
                {
                    return buf.ReadRetainedSlice(MessageShort);
                }
                break;

            default:
                return null;
        }

        return null;
    }
}
