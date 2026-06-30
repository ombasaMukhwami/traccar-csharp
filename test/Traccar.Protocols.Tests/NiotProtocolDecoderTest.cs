using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Protocols.Niot;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class NiotProtocolDecoderTest : ProtocolTestBase
{
    private NiotProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), NullLogger<NiotProtocolDecoder>.Instance);

    [Fact]
    public void TestDecode()
    {
        VerifyPosition(CreateDecoder(), Binary(
            "585880004c08675430347318522007161451458024b28003f566ee00000328f8000748217ffc500729007a280000000000160001383932353430323130363431363738373136323100050002004e00570d"),
            new DateTime(2020, 7, 16, 14, 51, 45, DateTimeKind.Utc), true, -1.33611, 36.89684);

        VerifyPosition(CreateDecoder(), Binary(
            "585880004c08675430355777182005201100468024121b03f390ba00000105f8000b8d207ffc5f0f290084500000000000160001383932353430323130363431363839323430303700050002004e55940d"));

        VerifyPosition(CreateDecoder(), Binary(
            "585880004C08640460465310081912101835080011679303C1E18F00400085F8014FBED87FFC4D15290085501A28000000160001383932353430323131313431323931333238343200050002004E55B40D"));
    }
}
