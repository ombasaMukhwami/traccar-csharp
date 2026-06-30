using Traccar.Protocols.Jt808;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class Jt808FrameDecoderTest : ProtocolTestBase
{
    [Fact]
    public void TestDecode()
    {
        VerifyFrame(
            new Jt808FrameDecoder(),
            Binary("283734303139303331313138352c312c3030312c454c4f434b2c332c35323934333929"),
            Binary("283734303139303331313138352c312c3030312c454c4f434b2c332c35323934333929"));

        VerifyFrame(
            new Jt808FrameDecoder(),
            Binary("7e307e087d557e"),
            Binary("7e307d02087d01557e"));

        VerifyFrame(
            new Jt808FrameDecoder(),
            Binary("7e307e087d557e"),
            Binary("24247e307d02087d01557e"));
    }
}
