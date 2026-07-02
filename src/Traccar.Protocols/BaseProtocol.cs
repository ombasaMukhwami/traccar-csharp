using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;

namespace Traccar.Protocols;

/// <summary>
/// Mirrors Java Traccar's BaseProtocol: each subclass declares its supported commands and registers
/// one or more connectors from its constructor. Port and timeout are injected as a resolved
/// ProtocolOptions instance — DI constructs each protocol via a factory that calls
/// IOptionsMonitor&lt;ProtocolOptions&gt;.Get(name) so the concrete POCO rather than the monitor
/// reaches the constructor.
/// </summary>
public abstract class BaseProtocol : IProtocol
{
    /// <summary>Derived from the class name like Java's BaseProtocol.nameFromClass (Gt06Protocol -> "gt06").</summary>
    public string Name { get; }

    private readonly ProtocolOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _supportedDataCommands = [];
    private readonly List<ITrackerConnector> _connectorList = [];

    protected BaseProtocol(ProtocolOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
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

    public IReadOnlyCollection<string> SupportedDataCommands => _supportedDataCommands;

    protected void SetSupportedDataCommands(params string[] commands) => _supportedDataCommands.AddRange(commands);

    public IReadOnlyCollection<ITrackerConnector> ConnectorList => _connectorList;

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
        _connectorList.Add(new ProtocolServer(
            _options.Name, _options.Port, datagram, _options.Timeout, configurePipeline,
            _loggerFactory.CreateLogger<ProtocolServer>()));
    }
}
