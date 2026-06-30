using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.GoSafe;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class GoSafeProtocolDecoderTest : ProtocolTestBase
{
    private GoSafeProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), NullLogger<GoSafeProtocolDecoder>.Instance);

    [Fact]
    public void TestDecodeBinaryPosition()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "f80601013fb82203661b2ee46249007a13003f45feefeeb401db1bbe00000000060e00d602018904036412080c010111e121003100410051010807000000000000004655f8"));
    }

    [Fact]
    public void TestDecodeMultiplePositionsInOneSentence()
    {
        var decoder = CreateDecoder();

        var positions = DecodeAll(decoder, Buffer(
            "*GS06,353218073585128,181255300523,,SYS:Smart Track;V9.31;V1.1.5,GPS:A;5;N31.551856;E74.366920;0;0;;2.15;2.64,COT:0,ADC:10.78;0.02,DTT:4002;E1;0;0;0;1$181325300523,,SYS:Smart Track;V9.31;V1.1.5,GPS:A;6;N31.551856;E74.366920;0;0;;2.05;2.13,COT:0,ADC:10.79;0.02,DTT:4002;E1;0;0;0;1#"));

        Assert.Equal(2, positions.Count);
        foreach (var position in positions)
        {
            Assert.True(position.Valid);
            Assert.Equal(31.551856, position.Latitude, 0.0001);
            Assert.Equal(74.366920, position.Longitude, 0.0001);
        }
    }

    [Fact]
    public void TestDecodeTemperatureSensorAttribute()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(decoder, Buffer(
            "*GS06,356449068350122,013519070819,,SYS:G6S;V3.37;V1.1.8,GPS:A;12;N23.169866;E113.450728;0;255;54;0.79,COT:18779;,ADC:12.66;0.58,DTT:4084;E1;0;0;0;1,IWD:0;1;ad031652643fff28;23.2;1;1;86031652504fff28;24.3;2;1;e603165252a5ff28;24.2;3;1;bb0416557da6ff28;24.0#"),
            Position.PrefixTemp + 3, 24.0);
    }

    [Fact]
    public void TestDecodeEventAttributeFromHexFragment()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(decoder, Buffer(
            "*GS06,359568052580548,091946150719,1C,SYS:G3C;V1.40;V1.0.4,GPS:A;5;S25.750200;E28.204858;0;0;1337;1.68,COT:,ADC:13.12;4.06,DTT:4004;C6;0;0;10000000;0$091948150719,,SYS:G3C;V1.40;V1.0.4,GPS:A;5;S25.750200;E28.204858;0;0;1337;1.68,COT:,ADC:12.96;4.06,DTT:4004;C6;0;0;0;1#"),
            Position.KeyEvent, 0x1C);
    }

    [Fact]
    public void TestDecodeOldFormatPosition()
    {
        var decoder = CreateDecoder();

        var position = Assert.IsType<Position>(Decode(decoder, Buffer(
            "*GS02,358696043774648,GPS:230040;A;S1.166829;E36.934287;0;0;170116,STT:20;0,MGR:32755204,ADC:0;11.2;1;28.3;2;4.1,GFS:0;0")));

        Assert.True(position.Valid);
        Assert.Equal(-1.166829, position.Latitude, 0.0001);
        Assert.Equal(36.934287, position.Longitude, 0.0001);
    }

    [Fact]
    public void TestDecodeIncompleteOldFormatIsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Buffer("*GS02,358696043774648"));
    }

    [Fact]
    public void TestDecodeGsmCellTower()
    {
        var decoder = CreateDecoder();

        VerifyNotNull(decoder, Buffer(
            "*GS06,359913060650380,101019050718,,SYS:G3C;V1.38;V05,GPS:L;6;N31.916576;E35.908480;0;0,GSM:1;4;416;3;627A;A84B;-66,COT:188,ADC:4.31;3.88,DTT:4005;E6;0;0;0;1#"));
    }
}
