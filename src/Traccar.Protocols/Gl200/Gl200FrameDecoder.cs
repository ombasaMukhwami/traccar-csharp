using System.Text;
using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Gl200;

public sealed class Gl200FrameDecoder : BaseFrameDecoder
{
    private const int MinimumLength = 11;

    private static readonly HashSet<string> BinaryHeaders =
    [
        "+RSP", "+BSP", "+EVT", "+BVT", "+INF", "+BNF", "+HBD", "+CRD", "+BRD", "+LGN",
    ];

    public static bool IsBinary(ByteBuf buf)
    {
        if (buf.GetByte(buf.ReaderIndex + 1) == 0)
        {
            return true;
        }
        var header = buf.ToString(buf.ReaderIndex, 4, Encoding.ASCII);
        if (header == "+ACK")
        {
            return buf.GetByte(buf.ReaderIndex + header.Length) != (byte)':';
        }
        return BinaryHeaders.Contains(header);
    }

    protected override object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buf)
    {
        if (buf.ReadableBytes < MinimumLength)
        {
            return null;
        }

        if (IsBinary(buf))
        {
            int length;
            if (buf.GetByte(buf.ReaderIndex + 1) == 0)
            {
                length = buf.GetUnsignedShort(buf.ReaderIndex + 2);
            }
            else
            {
                length = buf.ToString(buf.ReaderIndex, 4, Encoding.ASCII) switch
                {
                    "+ACK" => buf.GetUnsignedByte(buf.ReaderIndex + 6),
                    "+INF" or "+BNF" => buf.GetUnsignedShort(buf.ReaderIndex + 7),
                    "+HBD" => buf.GetUnsignedByte(buf.ReaderIndex + 5),
                    "+CRD" or "+BRD" or "+LGN" => buf.GetUnsignedShort(buf.ReaderIndex + 6),
                    _ => buf.GetUnsignedShort(buf.ReaderIndex + 9),
                };
            }

            if (buf.ReadableBytes >= length)
            {
                return buf.ReadRetainedSlice(length);
            }
        }
        else
        {
            var endIndex = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, (byte)'$');
            if (endIndex < 0)
            {
                endIndex = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, 0);
            }
            if (endIndex > 0)
            {
                var frame = buf.ReadRetainedSlice(endIndex - buf.ReaderIndex);
                buf.ReadByte(); // delimiter
                return frame;
            }
        }

        return null;
    }
}
