using Microsoft.Extensions.Logging;

namespace Traccar.Protocols;

/// <summary>
/// Mirrors Java's org.traccar.ServerManager: collects every registered protocol's connectors into
/// one flat list and starts/stops them together.
/// </summary>
public sealed class ServerManager : ILifecycleObject
{
    private readonly List<ITrackerConnector> connectorList = [];
    private readonly Dictionary<string, BaseProtocol> protocolList = [];
    private readonly ILogger<ServerManager> logger;

    public ServerManager(IEnumerable<BaseProtocol> protocols, ILogger<ServerManager> logger)
    {
        this.logger = logger;
        foreach (var protocol in protocols)
        {
            connectorList.AddRange(protocol.ConnectorList);
            protocolList[protocol.Name] = protocol;
        }
    }

    public BaseProtocol? GetProtocol(string name) => protocolList.GetValueOrDefault(name);

    public async Task StartAsync()
    {
        foreach (var connector in connectorList)
        {
            try
            {
                await connector.StartAsync();
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Port disabled due to conflict");
            }
        }
    }

    public async Task StopAsync()
    {
        foreach (var connector in connectorList)
        {
            await connector.StopAsync();
        }
    }
}
