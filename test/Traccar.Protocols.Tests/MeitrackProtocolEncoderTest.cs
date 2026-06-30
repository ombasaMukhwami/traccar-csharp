using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Model;
using Traccar.Protocols.Meitrack;
using Xunit;

namespace Traccar.Protocols.Tests;

public sealed class MeitrackProtocolEncoderTest : ProtocolTestBase
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder().Build();

    private MeitrackProtocolEncoder CreateEncoder()
        => new(Configuration, DbContextFactory, NullLogger<MeitrackProtocolEncoder>.Instance);

    [Fact]
    public void TestEncode()
    {
        var deviceId = SeedDevice("123456789012345");

        var command = new Command { DeviceId = deviceId, Type = Command.TypePositionSingle };
        Assert.Equal("@@A25,123456789012345,A10*58\r\n", Assert.IsType<string>(EncodeCommand(CreateEncoder(), command)));

        command = new Command { DeviceId = deviceId, Type = Command.TypeRequestPhoto };
        Assert.Equal(
            "@@A46,123456789012345,D03,1,camera_picture.jpg*1C\r\n",
            Assert.IsType<string>(EncodeCommand(CreateEncoder(), command)));

        command = new Command { DeviceId = deviceId, Type = Command.TypeSendSms };
        command.Set(Command.KeyPhone, "15360853789");
        command.Set(Command.KeyMessage, "Meitrack");
        Assert.Equal(
            "@@A48,123456789012345,C02,0,15360853789,Meitrack*8B\r\n",
            Assert.IsType<string>(EncodeCommand(CreateEncoder(), command)));
    }
}
