using DotNetty.Transport.Channels.Embedded;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Protocols.Jt1078;
using Traccar.Protocols.Media;
using Traccar.Protocols.Session;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class Jt1078ProtocolDecoderTest : ProtocolTestBase
{
    [Fact]
    public void TestDecodeSinglePacketFrame()
    {
        // 10-byte BCD device id, decoded (and looked up) raw, leading zeros included - JT1078's
        // device lookup mirrors Java's, which does not strip leading zeros the way Jt808ProtocolDecoder's
        // own Decode() does for its own messages.
        SeedDevice("00000866496077582164");

        var connectionManager = CreateConnectionManager();
        var deviceLookupService = new DeviceLookupService(DbContextFactory);
        var streamManager = new VideoStreamManager();
        var decoder = new Jt1078ProtocolDecoder(
            connectionManager, deviceLookupService, streamManager, NullLogger<Jt1078ProtocolDecoder>.Instance);

        var channel = new EmbeddedChannel(decoder);
        channel.WriteInbound(Binary(
            "3031636481e200760000086649607758216401100000000001c71cd5074a0021006700000001219a02f02f0fff000003000003008f6b47bab6659524c5f8fc61ed7c40a50a5c39629c000003016243bc0006e6de480021a5c4574cef0d7024c5e1f815212d007eac5e114b3c000003000003000004b0000003028574d2009ea480003e1bee06cf81e9"));

        // JT1078 never produces a Position - it's a video relay, not a GPS protocol.
        Assert.Null(channel.ReadInbound<object>());

        // The single-packet frame (subpackage type 0) should have reached the stream manager and
        // been muxed into at least one MPEG-TS segment, so the playlist is no longer the empty template.
        var playlist = streamManager.GetPlaylist(1, 1);
        Assert.Contains(".ts", playlist);
    }
}
