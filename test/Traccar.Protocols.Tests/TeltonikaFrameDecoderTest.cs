using Traccar.Protocols.Teltonika;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class TeltonikaFrameDecoderTest : ProtocolTestBase
{
    [Fact]
    public void TestDecode()
    {
        VerifyFrame(
            new TeltonikaFrameDecoder(),
            Binary("ff"),
            Binary("FF000F313233343536373839303132333435"));

        VerifyFrame(
            new TeltonikaFrameDecoder(),
            Binary("000F313233343536373839303132333435"),
            Binary("000F313233343536373839303132333435"));
    }
}
