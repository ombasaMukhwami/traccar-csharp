using DotNetty.Buffers;
using DotNetty.Transport.Channels.Embedded;
using Traccar.Protocols.Jt808;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class Jt808FrameEncoderTest : ProtocolTestBase
{
    [Fact]
    public void TestEncode()
    {
        var channel = new EmbeddedChannel(new Jt808FrameEncoder());
        channel.WriteOutbound(Binary("7e307e087d557e"));
        var result = Assert.IsAssignableFrom<IByteBuffer>(channel.ReadOutbound<object>());
        Assert.Equal("7e307d02087d01557e", ByteBufferUtil.HexDump(result));
    }
}
