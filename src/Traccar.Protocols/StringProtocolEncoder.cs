using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols;

public abstract class StringProtocolEncoder(
    string protocolName, IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger logger)
    : BaseProtocolEncoder(protocolName, dbContextFactory, logger)
{
    public delegate string? ValueFormatter(string key, object? value);

    /// <summary>
    /// Builds a command string from a composite-format template (using {0}, {1}, ... placeholders)
    /// and a list of attribute keys to substitute, in order. Mirrors Java's formatCommand helper.
    /// </summary>
    protected string FormatCommand(Command command, string format, ValueFormatter? valueFormatter, params string[] keys)
    {
        var values = new object[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            string? value;
            if (keys[i] == Command.KeyUniqueId)
            {
                value = GetUniqueId(command.DeviceId);
            }
            else
            {
                command.Attributes.TryGetValue(keys[i], out var attribute);
                value = valueFormatter?.Invoke(keys[i], attribute);
                if (value == null && attribute != null)
                {
                    value = attribute.ToString();
                }
                value ??= string.Empty;
            }
            values[i] = value;
        }

        return string.Format(format, values);
    }

    protected string FormatCommand(Command command, string format, params string[] keys)
        => FormatCommand(command, format, null, keys);
}
