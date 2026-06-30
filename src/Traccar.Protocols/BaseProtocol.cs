using DotNetty.Transport.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Traccar.Protocols;

/// <summary>
/// Mirrors Java Traccar's BaseProtocol implementing Protocol: each subclass declares its supported
/// commands and registers one or more connectors (one per transport/port) from its constructor.
/// The port for each protocol comes from configuration (Protocols:{name}:Port in appsettings.json),
/// mirroring Java's config.getInteger(Keys.PROTOCOL_PORT.withPrefix(protocol)).
/// </summary>
public abstract class BaseProtocol : IProtocol
{
    /// <summary>Derived from the class name like Java's BaseProtocol.nameFromClass (Gt06Protocol -> "gt06").</summary>
    public string Name { get; }

    private readonly IConfiguration configuration;
    private readonly ILoggerFactory loggerFactory;
    private readonly List<string> supportedDataCommands = [];
    private readonly List<ITrackerConnector> connectorList = [];

    protected BaseProtocol(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        this.configuration = configuration;
        this.loggerFactory = loggerFactory;
        Name = NameFromType(GetType());
    }

    /// <summary>Mirrors Java's BaseProtocol.nameFromClass (Gt06Protocol -> "gt06").</summary>
    public static string NameFromType(Type type)
    {
        var className = type.Name;
        return (className.EndsWith("Protocol", StringComparison.Ordinal)
            ? className[..^"Protocol".Length]
            : className).ToLowerInvariant();
    }

    public IReadOnlyCollection<string> SupportedDataCommands => supportedDataCommands;

    protected void SetSupportedDataCommands(params string[] commands) => supportedDataCommands.AddRange(commands);

    public IReadOnlyCollection<ITrackerConnector> ConnectorList => connectorList;

    /// <summary>Registers a TCP listener on this protocol's configured port.</summary>
    protected void AddServer(Action<IChannelPipeline> configurePipeline)
        => AddServer(datagram: false, configurePipeline);

    /// <summary>
    /// Registers a connector on this protocol's configured port, mirroring Java's addServer(new
    /// TrackerServer(config, getName(), datagram) {...}). Several protocols (H02, GoSafe, GL200,
    /// Teltonika) register both a TCP and a UDP listener on the same port in Java; call this twice
    /// (datagram: false, then true) to do the same here.
    /// </summary>
    protected void AddServer(bool datagram, Action<IChannelPipeline> configurePipeline)
    {
        var key = $"Protocols:{Name}:Port";
        var port = configuration.GetValue<int?>(key)
            ?? throw new InvalidOperationException($"Missing configuration value '{key}' for protocol '{Name}'");

        // Mirrors Java's BasePipelineFactory: protocol.timeout overrides server.timeout (default 1800s).
        var timeoutSeconds = configuration.GetValue<int?>($"Protocols:{Name}:Timeout")
            ?? configuration.GetValue("Server:Timeout", 1800);

        connectorList.Add(new ProtocolServer(
            Name, port, datagram, timeoutSeconds, configurePipeline, loggerFactory.CreateLogger<ProtocolServer>()));
    }
}
