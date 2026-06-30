using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.GoSafe;

public sealed class GoSafeFrameDecoder : BaseFrameDecoder
{
    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        var marker = (char)buf.GetByte(buf.ReaderIndex);

        if (marker == '*')
        {
            var index = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, (byte)'#');
            if (index != -1)
            {
                return buf.ReadRetainedSlice(index + 1 - buf.ReaderIndex);
            }
        }
        else
        {
            var index = buf.IndexOf(buf.ReaderIndex + 1, buf.WriterIndex, unchecked((byte)0xf8));
            if (index >= 0)
            {
                var result = Unpooled.Buffer(index + 1 - buf.ReaderIndex);
                while (buf.ReaderIndex <= index)
                {
                    var b = buf.ReadUnsignedByte();
                    if (b == 0x1b)
                    {
                        var ext = buf.ReadUnsignedByte();
                        if (ext == 0x00)
                        {
                            result.WriteByte(0x1b);
                        }
                        else if (ext == 0xe3)
                        {
                            result.WriteByte(0xf8);
                        }
                    }
                    else
                    {
                        result.WriteByte(b);
                    }
                }

                return result;
            }
        }

        return null;
    }
}
