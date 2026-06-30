using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Gt06;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class Gt06ProtocolEncoderTest : ProtocolTestBase
{
    [Fact]
    public void TestEngineStopEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = new Gt06ProtocolEncoder(DbContextFactory, NullLogger<Gt06ProtocolEncoder>.Instance);
        var command = new Command { DeviceId = deviceId, Type = Command.TypeEngineStop };

        Assert.Equal("787812800c0000000052656c61792c312300009dee0d0a", EncodeCommandAsHex(encoder, command));
    }
}
