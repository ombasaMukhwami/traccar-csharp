using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Protocols.H02;

public sealed class H02ProtocolEncoder(
    IConfiguration configuration, IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<H02ProtocolEncoder> logger)
    : StringProtocolEncoder("h02", dbContextFactory, logger)
{
    private const string Marker = "HQ";

    private static string FormatCommand(DateTime time, string uniqueId, string type, params string[] parameters)
    {
        var result = new StringBuilder($"*{Marker},{uniqueId},{type},{time:HHmmss}");
        foreach (var parameter in parameters)
        {
            result.Append(',').Append(parameter);
        }
        result.Append('#');
        return result.ToString();
    }

    internal object? EncodeCommand(Command command, DateTime time)
    {
        var uniqueId = GetUniqueId(command.DeviceId);

        return command.Type switch
        {
            Command.TypeAlarmArm => FormatCommand(time, uniqueId, "SCF", "0", "0"),
            Command.TypeAlarmDisarm => FormatCommand(time, uniqueId, "SCF", "1", "1"),
            Command.TypeEngineStop => FormatCommand(time, uniqueId, "S20", "1", "1"),
            Command.TypeEngineResume => FormatCommand(time, uniqueId, "S20", "1", "0"),
            Command.TypePositionPeriodic => configuration.GetValue<bool>(
                $"{ConfigKeys.Protocols.SectionPrefix}:{ProtocolName}:{ConfigKeys.Protocols.Alternative}")
                ? FormatCommand(time, uniqueId, "D1", command.Attributes[Command.KeyFrequency].ToString()!)
                : FormatCommand(time, uniqueId, "S71", "22", command.Attributes[Command.KeyFrequency].ToString()!),
            _ => null,
        };
    }

    protected override object? EncodeCommand(Command command) => EncodeCommand(command, DateTime.UtcNow);
}
