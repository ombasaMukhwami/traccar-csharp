using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.H02;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class H02ProtocolDecoderTest : ProtocolTestBase
{
    private H02ProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), CreateConfiguration(), NullLogger<H02ProtocolDecoder>.Instance);

    [Fact]
    public void TestDecodeV8WithExpectedCoordinates()
    {
        var decoder = CreateDecoder();

        VerifyPosition(
            decoder,
            Buffer("*HQ,9001000002,V8,213945,A,3542.2043,N,38.6508,W,0.00,170,221025,FBFFF9FF,0,0,0,0,22,31,126,0#"),
            new DateTime(2025, 10, 22, 21, 39, 45, DateTimeKind.Utc), true, 35.70340, -0.64418);
    }

    [Fact]
    public void TestDecodeV6Basic()
    {
        var decoder = CreateDecoder();

        VerifyPosition(
            decoder,
            Buffer("*HQ,3177718238,V6,002926,V,3514.4088,N,9733.2842,W,0.00,0.00,151222,FFF7FBFF,310,260,32936,13641,8944501311217563382F,#"));
    }

    [Fact]
    public void TestDecodeNegativeDegreeFormat()
    {
        var decoder = CreateDecoder();

        VerifyPosition(
            decoder,
            Buffer("*HQ,1451316409,V1,030149,A,-23-29.0095,S,-46-51.5852,W,2.4,065,070315,FFFFFFFF#"),
            new DateTime(2015, 3, 7, 3, 1, 49, DateTimeKind.Utc), true, -23.48349, -46.85975);
    }

    [Fact]
    public void TestDecodeHeartbeatBatteryAttribute()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(
            decoder, Buffer("*HQ,135790246811220,HTBT,100#"), Position.KeyBatteryLevel, 100);
    }

    [Fact]
    public void TestDecodeHeartbeatWithoutBatteryIsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Buffer("*HQ,135790246811220,HTBT#"));
    }

    [Fact]
    public void TestDecodeBinary()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "2491802711800850240512192350143206090249758e000001ffffbbff00bdf0900000000001d60161cc4b9a35"));
    }

    [Fact]
    public void TestDecodeBinaryWithExpectedCoordinates()
    {
        var decoder = CreateDecoder();

        VerifyPosition(
            decoder,
            Binary("24410600082621532131081504419390060740418306000000fffffbfdff0015060000002c02dc0c000000001f"),
            new DateTime(2015, 8, 31, 21, 53, 21, DateTimeKind.Utc), true, 4.69898, -74.06971);
    }

    [Fact]
    public void TestDecodeLbsRequest()
    {
        var decoder = CreateDecoder();

        VerifyNotNull(decoder, Buffer(
            "*HQ,4109179024,NBR,103732,722,310,0,6,8106,32010,23,8101,22007,25,8106,12010,23,8106,22105,22,8101,22012,16,8106,42010,5,100217,FFFFFBFF,5#"));
    }

    [Fact]
    public void TestDecodeLink()
    {
        var decoder = CreateDecoder();

        VerifyNotNull(decoder, Buffer("*HQ,1700086468,LINK,180902,15,0,84,0,0,240517,FFFFFBFF#"));
    }

    [Fact]
    public void TestDecodeV3()
    {
        var decoder = CreateDecoder();

        VerifyNotNull(decoder, Buffer(
            "*HQ,353111080001055,V3,044855,28403,01,001450,011473,158,-62,0292,0,X,030817,FFFFFBFF#"));
    }

    [Fact]
    public void TestDecodeVp1Cells()
    {
        var decoder = CreateDecoder();

        VerifyNotNull(decoder, Buffer("*hq,356327081001239,VP1,V,470,002,92,3565,0Y92,19433,30Y92,1340,29#"));
    }

    [Fact]
    public void TestDecodeVp1Position()
    {
        var decoder = CreateDecoder();

        VerifyNotNull(decoder, Buffer("*hq,356327080425330,VP1,A,2702.7245,S,15251.9311,E,0.48,0.0000,080917#"));
    }

    [Fact]
    public void TestDecodeSmsAttribute()
    {
        var decoder = CreateDecoder();

        const string expected =
            "ST906(70SACD)_TQ_V_2.0 2024/06/07\nID:5226073533\nIP:1.2.3.4 5013\nUT:30,30,300\n" +
            "VOLT:12.9V\nAPN:internet.example.com\nGPS:A-24-23\nGSM:26";

        VerifyAttribute(
            decoder,
            Buffer($"*HQ,5226073533,SMS,{expected}#"),
            Position.KeyResult, expected);
    }

    [Fact]
    public void TestDecodeStatusAttribute()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(
            decoder,
            Buffer("*HQ,2705171109,V1,213324,A,5002.5849,N,01433.7822,E,0.00,000,140613,FFFFFFFF#"),
            Position.KeyStatus, 0xFFFFFFFFL);
    }

    [Fact]
    public void TestDecodeEmptyFieldsIsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Buffer("*HQ,4109198974,#"));
    }

    [Fact]
    public void TestDecodeUnhandledXtTypeIsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Buffer("*HQ,356327080425330,XT,1,100#"));
    }

    [Fact]
    public void TestDecodeUnhandledBsTypeIsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Buffer(
            "*HQ,356803210091319,BS,,2d4,a,1b63,1969,26,1b63,10b2,31,0,0,25,,ffffffff,60#"));
    }

    [Fact]
    public void TestDecodeUnhandledBaseTypeIsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Buffer("*HQ,8401016597,BASE,152609,0,0,0,0,211014,FFFFFFFF#"));
    }

    [Fact]
    public void TestDecodeBinaryLongId()
    {
        var decoder = CreateDecoder();

        VerifyPosition(decoder, Binary(
            "2435248308419329301047591808172627335900074412294E024138FEFFFFFFFF01120064BA73005ECC"));
    }

    [Fact]
    public void TestDecodeStatusBinary()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(
            decoder,
            Binary("2441091144271222470112142233983006114026520E000000FFFFFBFFFF0014060000000001CC00262B0F170A"),
            Position.KeyStatus, 0xFFFFFBFFL);
    }

    [Fact]
    public void TestDecodeStatusWithCellId()
    {
        var decoder = CreateDecoder();

        VerifyAttribute(
            decoder,
            Buffer("*HQ,4210051415,V1,164549,A,0956.3869,N,08406.7068,W,000.00,000,221215,FFFFFBFF,712,01,0,0,6#"),
            Position.KeyStatus, 0xFFFFFBFFL);
    }
}
