using Microsoft.Extensions.Logging;

namespace Traccar.Protocols;

/// <summary>
/// Mirrors Java's org.traccar.ServerManager: collects every registered protocol's connectors into
/// one flat list and starts/stops them together.
/// </summary>
public sealed class ServerManager : ILifecycleObject
{
    private readonly List<ITrackerConnector> _connectorList = [];
    private readonly Dictionary<string, BaseProtocol> _protocolList = [];
    private readonly ILogger<ServerManager> _logger;

    public ServerManager(IEnumerable<BaseProtocol> protocols, ILogger<ServerManager> logger)
    {
        _logger = logger;
        foreach (var protocol in protocols)
        {
            _connectorList.AddRange(protocol.ConnectorList);
            _protocolList[protocol.Name] = protocol;
        }
    }

    public BaseProtocol? GetProtocol(string name) => _protocolList.GetValueOrDefault(name);

    public async Task StartAsync()
    {
        foreach (var connector in _connectorList)
        {
            try
            {
                await connector.StartAsync();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Port disabled due to conflict");
            }
        }
    }

    public async Task StopAsync()
    {
        foreach (var connector in _connectorList)
        {
            await connector.StopAsync();
        }
    }
}
