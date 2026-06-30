using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Teltonika;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class TeltonikaProtocolEncoderTest : ProtocolTestBase
{
    [Fact]
    public void TestCustomTextEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = new TeltonikaProtocolEncoder(DbContextFactory, NullLogger<TeltonikaProtocolEncoder>.Instance);
        var command = new Command { DeviceId = deviceId, Type = Command.TypeCustom };
        command.Set(Command.KeyData, "setdigout 11");

        Assert.Equal(
            "00000000000000140c01050000000c7365746469676f7574203131010000bed5",
            EncodeCommandAsHex(encoder, command));
    }

    [Fact]
    public void TestCustomHexEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = new TeltonikaProtocolEncoder(DbContextFactory, NullLogger<TeltonikaProtocolEncoder>.Instance);
        var command = new Command { DeviceId = deviceId, Type = Command.TypeCustom };
        command.Set(Command.KeyData, "03030000000185E8");

        Assert.Equal(
            "00000000000000100c01050000000803030000000185e8010000da8b",
            EncodeCommandAsHex(encoder, command));
    }
}
