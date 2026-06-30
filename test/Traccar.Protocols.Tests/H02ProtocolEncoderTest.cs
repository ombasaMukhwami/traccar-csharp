using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.H02;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class H02ProtocolEncoderTest : ProtocolTestBase
{
    private readonly DateTime time = new(2024, 1, 1, 1, 2, 3, DateTimeKind.Utc);

    private H02ProtocolEncoder CreateEncoder() => new(DbContextFactory, NullLogger<H02ProtocolEncoder>.Instance);

    [Fact]
    public void TestAlarmArmEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = CreateEncoder();
        var command = new Command { DeviceId = deviceId, Type = Command.TypeAlarmArm };

        Assert.Equal("*HQ,123456789012345,SCF,010203,0,0#", encoder.EncodeCommand(command, time));
    }

    [Fact]
    public void TestAlarmDisarmEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = CreateEncoder();
        var command = new Command { DeviceId = deviceId, Type = Command.TypeAlarmDisarm };

        Assert.Equal("*HQ,123456789012345,SCF,010203,1,1#", encoder.EncodeCommand(command, time));
    }

    [Fact]
    public void TestEngineStopEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = CreateEncoder();
        var command = new Command { DeviceId = deviceId, Type = Command.TypeEngineStop };

        Assert.Equal("*HQ,123456789012345,S20,010203,1,1#", encoder.EncodeCommand(command, time));
    }

    [Fact]
    public void TestEngineResumeEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = CreateEncoder();
        var command = new Command { DeviceId = deviceId, Type = Command.TypeEngineResume };

        Assert.Equal("*HQ,123456789012345,S20,010203,1,0#", encoder.EncodeCommand(command, time));
    }

    [Fact]
    public void TestPositionPeriodicEncode()
    {
        var deviceId = SeedDevice("123456789012345");
        var encoder = CreateEncoder();
        var command = new Command { DeviceId = deviceId, Type = Command.TypePositionPeriodic };
        command.Set(Command.KeyFrequency, 10);

        Assert.Equal("*HQ,123456789012345,S71,010203,22,10#", encoder.EncodeCommand(command, time));
    }
}
