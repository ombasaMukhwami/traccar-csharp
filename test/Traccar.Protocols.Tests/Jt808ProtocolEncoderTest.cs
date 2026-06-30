using DotNetty.Buffers;
using DotNetty.Transport.Channels.Embedded;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Jt808;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class Jt808ProtocolEncoderTest : ProtocolTestBase
{
    private Jt808ProtocolDecoder CreateDecoder()
        => new(CreateConnectionManager(), CreateConfiguration(), NullLogger<Jt808ProtocolDecoder>.Instance);

    private Jt808ProtocolEncoder CreateEncoder()
        => new(CreateConfiguration(), DbContextFactory, NullLogger<Jt808ProtocolEncoder>.Instance);

    private static IByteBuffer EncodeCommand(EmbeddedChannel channel, Command command)
    {
        channel.WriteOutbound(command);
        return Assert.IsAssignableFrom<IByteBuffer>(channel.ReadOutbound<object>());
    }

    [Fact]
    public void TestEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = CreateEncoder();
        var channel = new EmbeddedChannel(CreateDecoder(), encoder);

        var command = new Command { DeviceId = deviceId, Type = Command.TypeEngineStop };

        var result = EncodeCommand(channel, command);
        Assert.Equal("7e810500010b3a73ce2ff20000f0247e", ByteBufferUtil.HexDump(result));

        command.Type = Command.TypeCustom;
        command.Set(Command.KeyData, "7e830000140b3a73ce2ff2000001546573742c20436f6d6d616e642c2031323323a57e");
        result = EncodeCommand(channel, command);
        Assert.Equal("7e830000140b3a73ce2ff2000001546573742c20436f6d6d616e642c2031323323a57e", ByteBufferUtil.HexDump(result));

        encoder.SetModelOverride("BSJ");

        command.Set(Command.KeyData, "Test, Command, 123#");
        result = EncodeCommand(channel, command);
        Assert.Equal("7e830000140b3a73ce2ff2000001546573742c20436f6d6d616e642c2031323323a57e", ByteBufferUtil.HexDump(result));
    }

    [Fact]
    public void TestEncodeJimiCustom()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = CreateEncoder();
        encoder.SetModelOverride("JC371");
        var channel = new EmbeddedChannel(CreateDecoder(), encoder);

        var command = new Command { DeviceId = deviceId, Type = Command.TypeCustom };
        command.Set(Command.KeyData, "TEST");

        var result = EncodeCommand(channel, command);
        Assert.Equal("7e890000050b3a73ce2ff20000f0544553543b7e", ByteBufferUtil.HexDump(result));
    }
}
