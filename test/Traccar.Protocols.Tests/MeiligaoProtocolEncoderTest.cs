using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Meiligao;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class MeiligaoProtocolEncoderTest : ProtocolTestBase
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder().Build();

    private MeiligaoProtocolEncoder CreateEncoder()
        => new(Configuration, DbContextFactory, NullLogger<MeiligaoProtocolEncoder>.Instance);

    [Fact]
    public void TestEncode()
    {
        var deviceId = SeedDevice("12345678901234");

        var command = new Command { DeviceId = deviceId, Type = Command.TypePositionSingle };
        Assert.Equal("404000111234567890123441016cf70d0a", EncodeCommandAsHex(CreateEncoder(), command));

        command = new Command { DeviceId = deviceId, Type = Command.TypePositionPeriodic };
        command.Set(Command.KeyFrequency, 100);
        Assert.Equal("40400013123456789012344102000a2f4f0d0a", EncodeCommandAsHex(CreateEncoder(), command));

        command = new Command { DeviceId = deviceId, Type = Command.TypeSetTimezone };
        command.Set(Command.KeyTimezone, "GMT+8");
        Assert.Equal("4040001412345678901234413234383030ad0d0a", EncodeCommandAsHex(CreateEncoder(), command));

        command = new Command { DeviceId = deviceId, Type = Command.TypeRebootDevice };
        Assert.Equal("40400011123456789012344902d53d0d0a", EncodeCommandAsHex(CreateEncoder(), command));

        command = new Command { DeviceId = deviceId, Type = Command.TypeAlarmGeofence };
        command.Set(Command.KeyRadius, 1000);
        Assert.Equal("4040001312345678901234410603e87bb00d0a", EncodeCommandAsHex(CreateEncoder(), command));

        command = new Command { DeviceId = deviceId, Type = Command.TypeEngineStop };
        Assert.Equal("4040001212345678901234411501fd460d0a", EncodeCommandAsHex(CreateEncoder(), command));
    }
}
