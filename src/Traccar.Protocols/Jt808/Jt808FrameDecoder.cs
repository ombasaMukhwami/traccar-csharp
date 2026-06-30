using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Jt808;

public sealed class Jt808FrameDecoder() : BaseFrameDecoder(LargeMaxFrameLength)
{
    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        while (buf.IsReadable())
        {
            var b = buf.GetUnsignedByte(buf.ReaderIndex);
            if (b == '(' || b == 0x7e || b == 0xe7)
            {
                break;
            }
            buf.SkipBytes(1);
        }

        if (buf.ReadableBytes < 2)
        {
            return null;
        }

        var first = buf.GetUnsignedByte(buf.ReaderIndex);

        if (first == '(')
        {
            var index = buf.IndexOf(buf.ReaderIndex + 1, buf.WriterIndex, (byte)')');
            if (index >= 0)
            {
                return buf.ReadRetainedSlice(index + 1 - buf.ReaderIndex);
            }
        }
        else
        {
            var delimiter = first;
            var alternative = delimiter == 0xe7;

            var index = buf.IndexOf(buf.ReaderIndex + 1, buf.WriterIndex, unchecked((byte)delimiter));
            if (index >= 0)
            {
                var result = Unpooled.Buffer(index + 1 - buf.ReaderIndex);

                while (buf.ReaderIndex <= index)
                {
                    var b = buf.ReadUnsignedByte();
                    if (alternative && (b == 0xe6 || b == 0x3e))
                    {
                        var ext = buf.ReadUnsignedByte();
                        if (b == 0xe6 && ext == 0x01)
                        {
                            result.WriteByte(0xe6);
                        }
                        else if (b == 0xe6 && ext == 0x02)
                        {
                            result.WriteByte(0xe7);
                        }
                        else if (b == 0x3e && ext == 0x01)
                        {
                            result.WriteByte(0x3e);
                        }
                        else if (b == 0x3e && ext == 0x02)
                        {
                            result.WriteByte(0x3d);
                        }
                    }
                    else if (!alternative && b == 0x7d)
                    {
                        var ext = buf.ReadUnsignedByte();
                        if (ext == 0x01)
                        {
                            result.WriteByte(0x7d);
                        }
                        else if (ext == 0x02)
                        {
                            result.WriteByte(0x7e);
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
