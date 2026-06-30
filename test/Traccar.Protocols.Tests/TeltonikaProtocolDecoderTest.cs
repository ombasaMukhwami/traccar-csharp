using DotNetty.Transport.Channels.Embedded;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Teltonika;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class TeltonikaProtocolDecoderTest : ProtocolTestBase
{
    private const string ImeiHandshake = "000F313233343536373839303132333435";

    private TeltonikaProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), NullLogger<TeltonikaProtocolDecoder>.Instance, CreateConfiguration(), connectionless: false);

    private static void Authenticate(EmbeddedChannel channel)
    {
        channel.WriteInbound(Binary(ImeiHandshake));
        channel.ReadInbound<object>();
    }

    private static List<Position> DecodeAuthenticated(TeltonikaProtocolDecoder decoder, string hex)
    {
        var channel = new EmbeddedChannel(decoder);
        Authenticate(channel);

        channel.WriteInbound(Binary(hex));
        var results = new List<Position>();
        object? item;
        while ((item = channel.ReadInbound<object>()) != null)
        {
            results.Add(Assert.IsType<Position>(item));
        }
        return results;
    }

    [Fact]
    public void TestDecodeImeiHandshakeIsNull()
    {
        var decoder = CreateDecoder();

        VerifyNull(decoder, Binary(ImeiHandshake));
    }

    [Fact]
    public void TestDecodeCodec8ExtPosition()
    {
        var positions = DecodeAuthenticated(CreateDecoder(),
            "000000000000010e8e020000019769de9f9800015299f718b278040018007708000000000013000b00ef0100f00000150500c80000450100010100b30000020000030000b401017c00000500b5001a00b6000c004238d40043000000440000000200f10000539b00100000efc70001004e000000000000000000000000019769de5d3b00015299f718b27804001800770800002c350001000000000000000000012c3500700124050f4e65766572615f33000000000000000f067cd9f4110c4006020c8f0701340e020c1c24050f4e65766572615f31000000000000000f067cd9f411334606020c380701340e020c2624050f4e65766572615f32000000000000000f067cd9f411464706020c240701360e020c26020000a8b0");

        Assert.Equal(2, positions.Count);
        foreach (var position in positions)
        {
            Assert.True(position.Valid);
            Assert.InRange(position.Latitude, -90, 90);
            Assert.InRange(position.Longitude, -180, 180);
        }
    }

    [Fact]
    public void TestDecodeCodec13DriverUniqueId()
    {
        var positions = DecodeAuthenticated(CreateDecoder(),
            "00000000000000240d01060000001c642b3ad14754534c7c367c317c307c31323734393838347c317c0d0a010000ec11");

        var position = Assert.Single(positions);
        Assert.Equal("12749884", position.Attributes[Position.KeyDriverUniqueId]);
    }

    [Fact]
    public void TestDecodeSingleAvlRecord()
    {
        var positions = DecodeAuthenticated(CreateDecoder(),
            "00000000000000a38e0100000178b11c9e1000040bbbc91f03190c002100e113001300000010000600ef0100f00100150300c800004501001e070005004237e00018000a00ce166200430f970024059f000300f10000665900c70000001e00100000a1b500000002010000115756315a5a5a32455a38363031353338380119002950303239392c50303637352c50303637342c50303637322c50303637312c50303437312c50324241430100001ad8");

        var position = Assert.Single(positions);
        Assert.True(position.Valid);
        Assert.InRange(position.Latitude, -90, 90);
        Assert.InRange(position.Longitude, -180, 180);
    }
}
