using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Protocols.Meitrack;

public sealed class MeitrackProtocolEncoder(
    IConfiguration configuration, IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<MeitrackProtocolEncoder> logger)
    : StringProtocolEncoder("meitrack", dbContextFactory, logger)
{
    private string FormatCommand(Command command, string content)
    {
        var uniqueId = GetUniqueId(command.DeviceId);
        var length = 1 + uniqueId.Length + 1 + content.Length + 5;
        var result = $"@@A{length:D2},{uniqueId},{content}*";
        result += Checksum.Sum(result) + "\r\n";
        return result;
    }

    protected override object? EncodeCommand(Command command)
    {
        var alternative = configuration.GetValue<bool>("Protocols:meitrack:Alternative");

        return command.Type switch
        {
            Command.TypeCustom => FormatCommand(command, command.GetString(Command.KeyData) ?? string.Empty),
            Command.TypePositionSingle => FormatCommand(command, "A10"),
            Command.TypeEngineStop => FormatCommand(command, "C01,0,12222"),
            Command.TypeEngineResume => FormatCommand(command, "C01,0,02222"),
            Command.TypeAlarmArm => FormatCommand(command, alternative ? "B21,1" : "C01,0,22122"),
            Command.TypeAlarmDisarm => FormatCommand(command, alternative ? "B21,0" : "C01,0,22022"),
            Command.TypeRequestPhoto => FormatCommand(
                command, $"D03,{(command.GetInteger(Command.KeyIndex) is var index and > 0 ? index : 1)},camera_picture.jpg"),
            Command.TypeSendSms => FormatCommand(
                command, $"C02,0,{command.GetString(Command.KeyPhone)},{command.GetString(Command.KeyMessage)}"),
            _ => null,
        };
    }
}
