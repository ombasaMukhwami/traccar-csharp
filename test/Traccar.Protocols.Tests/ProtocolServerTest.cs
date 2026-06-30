using System.Net;
using System.Net.Sockets;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Traccar.Protocols;

namespace Traccar.Protocols.Tests;

public class ProtocolServerTest
{
    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task IdleTimeout_ClosesConnectionAfterTimeoutElapses()
    {
        var port = GetFreePort();
        var server = new ProtocolServer(
            "test", port, datagram: false, timeoutSeconds: 1,
            _ => { }, NullLogger<ProtocolServer>.Instance);
        await server.StartAsync();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            var stream = client.GetStream();

            var buffer = new byte[16];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var read = await stream.ReadAsync(buffer, cts.Token);

            Assert.Equal(0, read); // 0 bytes read on a graceful close initiated by the server
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_ClosesOpenConnectionsEvenWithoutIdleTimeout()
    {
        var port = GetFreePort();
        var server = new ProtocolServer(
            "test", port, datagram: false, timeoutSeconds: 0,
            _ => { }, NullLogger<ProtocolServer>.Instance);
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();

        // Give the server's event loop a moment to fire ChannelActive and register the
        // connection in ProtocolServer's tracked-channels set before triggering shutdown.
        await Task.Delay(200);

        await server.StopAsync();

        var buffer = new byte[16];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var read = await stream.ReadAsync(buffer, cts.Token);

        Assert.Equal(0, read); // server-tracked connection should be force-closed on shutdown
    }
}
