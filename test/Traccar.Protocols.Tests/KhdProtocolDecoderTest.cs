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

    [Fact]
    public void TestDecodePosition2()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "292980002825863156210105095059035109370460010100000211ffff000002fc0000001e780b12000034e70d"));
    }

    [Fact]
    public void TestDecodePosition3()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "2929a3003420b2ab46201115115601800115110350825100000133fb00df4bfdff0d000000000000000900000c180887d9ffffffffffff960d"));
    }

    [Fact]
    public void TestDecodePosition4()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "2929a3002e1780c663170216203353003060811013839500000114f8000000ffff5000000a00000000000000060102003db70d"));
    }

    [Fact]
    public void TestDecodePosition5()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "292980002805162935140108074727801129670365336900000103ffff000082fc0000001e78091b000000360d"));
    }

    [Fact]
    public void TestDecodePosition6()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "29298100280A9F9538081228160131022394301140372500000330FF0000007FFC0F00001E000000000034290D"));
    }

    [Fact]
    public void TestDecodePosition7()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "29298000280A81850A120310095750005281370061190800000232F848FFBBFFFF0000001E000000000000ED0D"));
    }

    [Fact]
    public void TestDecodePosition8()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "29298E00280F80815A121218203116022318461140227000720262FB00077C7FBF5600001E3C3200000000850D"));
    }

    [Fact]
    public void TestDecodeReplyMessage()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "29298500081DD08C22120312174026026545710312541700000000F819C839FFFF1D00001E00500000003AF90D"));
    }

    [Fact]
    public void TestDecodePosition9()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "292980002822836665140825142037045343770193879200000050ffff000082fc000004b0780b170000002a0d"));
    }

    [Fact]
    public void TestDecodePosition10()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "292980002802425349120811032137022373011140211100000334FFFF000082FC0000001E780913000034DF0D"));
    }
}
