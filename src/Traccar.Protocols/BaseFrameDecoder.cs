using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols;

public abstract class BaseFrameDecoder(int maxFrameLength = BaseFrameDecoder.DefaultMaxFrameLength) : ByteToMessageDecoder
{
    public const int DefaultMaxFrameLength = 1024;
    public const int LargeMaxFrameLength = 32 * 1024;

    protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
    {
        var decoded = Decode(context, context.Channel, new ByteBuf(input));
        if (decoded != null)
        {
            output.Add(decoded);
        }
        else if (input.ReadableBytes > maxFrameLength)
        {
            throw new TooLongFrameException($"Frame exceeds {maxFrameLength} bytes");
        }
    }

    protected abstract object? Decode(IChannelHandlerContext context, IChannel channel, ByteBuf buffer);
}
