using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Gl200;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class Gl200ProtocolDecoderTest : ProtocolTestBase
{
    private Gl200ProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), NullLogger<Gl200ProtocolDecoder>.Instance);

    [Fact]
    public void TestDecodeFriPosition()
    {
        var decoder = CreateDecoder();

        var positions = DecodeAll(decoder, Buffer(
            "+RESP:GTFRI,DF0200,868487004353181,cv100,14051,10,1,0,0.0,0,264.1,114.015515,22.537178,20210608064328,0460,0001,25F8,061A7D02,,0.0,,,,100,21,,,,20210608144354,32DB$"));

        var position = Assert.Single(positions);
        Assert.True(position.Valid);
        Assert.Equal(114.015515, position.Longitude, 0.00001);
        Assert.Equal(22.537178, position.Latitude, 0.00001);
        Assert.Equal(new DateTime(2021, 6, 8, 6, 43, 28, DateTimeKind.Utc), position.FixTime);
    }

    [Fact]
    public void TestDecodeRtlPosition()
    {
        var decoder = CreateDecoder();

        var position = Assert.IsType<Position>(Decode(decoder, Buffer(
            "+RESP:GTRTL,DF0200,868487004353181,cv100,,00,1,0,0.0,0,102.2,114.015295,22.537250,20210608063942,0460,0001,25F8,061A7D02,,0.0,20210608143939,32CF$")));

        Assert.True(position.Valid);
        Assert.Equal(114.015295, position.Longitude, 0.00001);
        Assert.Equal(22.537250, position.Latitude, 0.00001);
    }

    [Fact]
    public void TestDecodeSosAlarm()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(decoder, Buffer(
            "+RESP:GTSOS,DF0200,868487004358800,cv100,,00,1,1,0.0,0,138.0,114.015465,22.537372,20210714115224,0460,0001,25F8,061A7D02,,,20210714195224,20210714195224,03A6$"),
            Position.KeyAlarm, Position.AlarmSos);
    }

    [Fact]
    public void TestDecodeFriWithMultipleLocations()
    {
        var decoder = CreateDecoder();

        var positions = DecodeAll(decoder, Buffer(
            "+RESP:GTFRI,1A0900,860599000306845,G3-313,0,0,4,1,2.1,0,426.7,8.611466,47.681639,20181214134603,0228,0001,077F,4812,25.2,1,5.7,34,437.3,8.611600,47.681846,20181214134619,0228,0001,077F,4812,25.2,1,4.4,62,438.2,8.611893,47.681983,20181214134633,0228,0001,077F,4812,25.2,1,4.8,78,436.6,8.612236,47.682040,20181214134648,0228,0001,077F,4812,25.2,83,20181214134702,0654$"));

        Assert.True(positions.Count >= 1);
        foreach (var position in positions)
        {
            Assert.True(position.Valid);
            Assert.InRange(position.Latitude, -90, 90);
            Assert.InRange(position.Longitude, -180, 180);
        }
    }

    [Fact]
    public void TestDecodeIglIgnitionEvent()
    {
        var decoder = CreateDecoder();

        VerifyNotNull(decoder, Buffer(
            "+RESP:GTIGL,DF0200,868487004353181,cv100,,00,1,1,0.0,0,264.8,114.015502,22.537327,20210608064027,0460,0001,25F8,061A7D02,,0.0,20210608144025,32D1$"));
    }
}
