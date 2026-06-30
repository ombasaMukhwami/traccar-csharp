using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols.Gl200;

public sealed class Gl200ProtocolEncoder(IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<Gl200ProtocolEncoder> logger)
    : StringProtocolEncoder("gl200", dbContextFactory, logger)
{
    protected override object? EncodeCommand(Command command)
    {
        InitDevicePassword(command, "");

        return command.Type switch
        {
            Command.TypePositionSingle => FormatCommand(
                command, "AT+GTRTO={0},1,,,,,,FFFF$", Command.KeyDevicePassword),
            Command.TypeEngineStop => FormatCommand(
                command, "AT+GTOUT={0},1,,,0,0,0,0,0,0,0,,,,,,,FFFF$", Command.KeyDevicePassword),
            Command.TypeEngineResume => FormatCommand(
                command, "AT+GTOUT={0},0,,,0,0,0,0,0,0,0,,,,,,,FFFF$", Command.KeyDevicePassword),
            Command.TypeIdentification => FormatCommand(
                command, "AT+GTRTO={0},8,,,,,,FFFF$", Command.KeyDevicePassword),
            Command.TypeRebootDevice => FormatCommand(
                command, "AT+GTRTO={0},3,,,,,,FFFF$", Command.KeyDevicePassword),
            _ => null,
        };
    }
}
