using DotNetty.Transport.Channels.Embedded;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Gt06;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class Gt06ProtocolDecoderTest : ProtocolTestBase
{
    private Gt06ProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), NullLogger<Gt06ProtocolDecoder>.Instance);

    private static object? Login(EmbeddedChannel channel, string hex)
    {
        channel.WriteInbound(Binary(hex));
        return channel.ReadInbound<object>();
    }

    [Fact]
    public void TestDecodeLoginIsNull()
    {
        var channel = new EmbeddedChannel(CreateDecoder());
        Assert.Null(Login(channel, "78780D01086471700328358100093F040D0A"));
    }

    [Fact]
    public void TestDecodeShortFrameIsNull()
    {
        var channel = new EmbeddedChannel(CreateDecoder());
        Assert.Null(Login(channel, "787805120099abec0d0a"));
    }

    [Fact]
    public void TestDecodeGpsPositionWithExpectedCoordinates()
    {
        var channel = new EmbeddedChannel(CreateDecoder());
        Login(channel, "78780D01086471700328358100093F040D0A");

        channel.WriteInbound(Binary("78781f120f0a140e150bc505e51e780293a9e800540000f601006e0055da00035f240d0a"));
        var position = Assert.IsType<Position>(channel.ReadInbound<object>());

        Assert.Equal(new DateTime(2015, 10, 20, 14, 21, 11, DateTimeKind.Utc), position.FixTime);
        Assert.True(position.Valid);
        Assert.Equal(54.94535, position.Latitude, 0.001);
        Assert.Equal(24.01762, position.Longitude, 0.001);
    }

    [Fact]
    public void TestDecodeGpsLbsPosition()
    {
        var channel = new EmbeddedChannel(CreateDecoder());
        Login(channel, "78780D01086471700328358100093F040D0A");

        channel.WriteInbound(Binary("787823120f081b121d37cb01c8e2cc08afd3c020d50201940701d600a1190041ee100576d1470d0a"));
        var position = Assert.IsType<Position>(channel.ReadInbound<object>());

        Assert.InRange(position.Latitude, -90, 90);
        Assert.InRange(position.Longitude, -180, 180);
    }

    [Fact]
    public void TestDecodeHeartbeatIsNotNull()
    {
        var channel = new EmbeddedChannel(CreateDecoder());
        Login(channel, "78780D01086471700328358100093F040D0A");

        // MSG_HEARTBEAT (0x23): header(2) + length(06) + type(23) + status(01) + index(0001) + crc(0000) + stop(0d0a).
        channel.WriteInbound(Binary("7878062301000100000d0a"));
        Assert.IsType<Position>(channel.ReadInbound<object>());
    }

    [Fact]
    public void TestDecodeStatusMessage()
    {
        var channel = new EmbeddedChannel(CreateDecoder());
        Login(channel, "78780D01086471700328358100093F040D0A");

        channel.WriteInbound(Binary("78780a13440604000201baaf540d0a"));
        Assert.NotNull(channel.ReadInbound<object>());
    }
}
