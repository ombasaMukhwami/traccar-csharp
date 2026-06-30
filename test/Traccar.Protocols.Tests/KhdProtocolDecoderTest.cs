using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Khd;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class KhdProtocolDecoderTest : ProtocolTestBase
{
    private KhdProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), NullLogger<KhdProtocolDecoder>.Instance);

    [Fact]
    public void TestDecodeDriverUniqueIdAttribute()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(decoder, Binary(
            "2929A300403099934C2004030943310000000000000000000000007B0000007FFF0E0000E70014000000000018050B01303030314330334437312102007B2203140DDA610D"),
            Position.KeyDriverUniqueId, "0001C03D71");
    }

    [Fact]
    public void TestDecodeBatteryLevelAttribute()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(decoder, Binary(
            "2929a3003e1680ba0a2304180759500000000000000000000000007b00000080001914000000000000000000162001641b0b0000249002bc58030001cc46020000e70d"),
            Position.KeyBatteryLevel, 100);
    }

    [Fact]
    public void TestDecodePositionUpload()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "2929800028258b8c10210731035840031534240542120200000337fb000000ffff5a00000a0000000005005d0d"));
    }

    [Fact]
    public void TestDecodePositionReupload()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "29298e006d1f29402d181117083846801193910365274500000000f80000227ffc3f00001e00500000000000060088000000220019ffc100000000000000000000000000000000007080002000000016ff893839323534303231303734313134323334333639000800233030302e30306e0d"));
    }

    [Fact]
    public void TestDecodeAlarmMessage()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "29298200230AA2CC391205030505220285947903109550008002078400000002000000000000750D"));
    }

    [Fact]
    public void TestDecodeLoginReturnsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Binary("2929b1000605162935b80d"));
    }
}
