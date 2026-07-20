using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace Traccar.Protocols.Forward;

/// <summary>
/// Forwards decoded positions to a remote SignalR hub.
/// The hub URL comes from Forward:Url; the server-side method name from Forward:Topic
/// (default "PositionReceived"). The connection is started lazily on the first send and
/// re-established automatically via WithAutomaticReconnect.
/// </summary>
public sealed class PositionForwarderSignalR : IPositionForwarder, IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly string _method;

    public PositionForwarderSignalR(IConfiguration configuration)
    {
        var url = configuration[ConfigKeys.Forward.Url]
            ?? throw new InvalidOperationException("Forward:Url is required for SignalR forwarding.");

        _method = configuration[ConfigKeys.Forward.Topic] ?? ConfigKeys.Forward.DefaultSignalRMethod;

        _connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();
    }

    public void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler)
    {
        EnsureConnectedAsync()
            .ContinueWith(connectTask =>
            {
                if (connectTask.IsFaulted)
                {
                    resultHandler(false, connectTask.Exception?.GetBaseException());
                    return;
                }
                _connection.SendAsync(_method, data)
                    .ContinueWith(sendTask =>
                        resultHandler(!sendTask.IsFaulted, sendTask.Exception?.GetBaseException()));
            });
    }

    private Task EnsureConnectedAsync()
    {
        if (_connection.State == HubConnectionState.Connected)
            return Task.CompletedTask;

        if (_connection.State == HubConnectionState.Disconnected)
            return _connection.StartAsync();

        // Reconnecting or connecting already — poll until settled.
        return WaitForConnectionAsync();
    }

    private async Task WaitForConnectionAsync()
    {
        while (_connection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            await Task.Delay(100);

        if (_connection.State != HubConnectionState.Connected)
            await _connection.StartAsync();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
