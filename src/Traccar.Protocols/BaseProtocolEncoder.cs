using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols;

/// <summary>
/// Outbound handler that intercepts <see cref="Command"/> writes and encodes them into the
/// protocol's wire format, mirroring Java Traccar's BaseProtocolEncoder.
/// </summary>
public abstract class BaseProtocolEncoder(
    string protocolName, IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger logger) : ChannelHandlerAdapter
{
    public string ProtocolName { get; } = protocolName;

    protected ILogger Logger { get; } = logger;

    public override Task WriteAsync(IChannelHandlerContext context, object message)
    {
        if (message is not Command command)
        {
            return context.WriteAsync(message);
        }

        var encoded = EncodeCommand(context.Channel, command);
        Logger.LogInformation(
            "{Protocol} command type: {Type} {Result}", ProtocolName, command.Type, encoded != null ? "sent" : "not sent");

        return encoded != null ? context.WriteAsync(encoded) : Task.CompletedTask;
    }

    /// <summary>
    /// Sets the command's device-password attribute to a default if not already present. Java looks
    /// up a per-device override via config; without a config system this just applies the default.
    /// </summary>
    protected static void InitDevicePassword(Command command, string defaultPassword)
    {
        if (!command.HasAttribute(Command.KeyDevicePassword))
        {
            command.Set(Command.KeyDevicePassword, defaultPassword);
        }
    }

    protected string GetUniqueId(long deviceId)
    {
        using var db = dbContextFactory.CreateDbContext();
        return db.Devices.Find(deviceId)?.UniqueId ?? string.Empty;
    }

    private string? _modelOverride;

    /// <summary>Forces GetDeviceModel to report this model regardless of the device's actual one.</summary>
    public void SetModelOverride(string? modelOverride) => _modelOverride = modelOverride;

    protected string? GetDeviceModel(long deviceId)
    {
        if (_modelOverride != null)
        {
            return _modelOverride;
        }
        using var db = dbContextFactory.CreateDbContext();
        return db.Devices.Find(deviceId)?.Model;
    }

    protected virtual object? EncodeCommand(IChannel channel, Command command) => EncodeCommand(command);

    protected virtual object? EncodeCommand(Command command) => null;
}
