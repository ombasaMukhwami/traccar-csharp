using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Khd;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class KhdProtocolEncoderTest : ProtocolTestBase
{
    [Fact]
    public void TestEngineStopEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = new KhdProtocolEncoder(DbContextFactory, NullLogger<KhdProtocolEncoder>.Instance);
        var command = new Command { DeviceId = deviceId, Type = Command.TypeEngineStop };

        Assert.Equal("29293900065981972d5d0d", EncodeCommandAsHex(encoder, command));
    }

    [Fact]
    public void TestPositionSingleEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = new KhdProtocolEncoder(DbContextFactory, NullLogger<KhdProtocolEncoder>.Instance);
        var command = new Command { DeviceId = deviceId, Type = Command.TypePositionSingle };

        Assert.Equal("29293000065981972d540d", EncodeCommandAsHex(encoder, command));
    }
}
