using System.Collections.Concurrent;
using System.Net;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;

namespace Traccar.Protocols;

/// <summary>
/// Binds either a TCP listener or a UDP datagram socket for a protocol, mirroring Java's
/// TrackerServer(Config, String protocol, boolean datagram) constructor.
/// </summary>
public sealed class ProtocolServer(
    string name, int port, bool datagram, int timeoutSeconds,
    Action<IChannelPipeline> configurePipeline, ILogger<ProtocolServer> logger)
    : ITrackerConnector
{
    private readonly ConcurrentDictionary<IChannel, byte> _channels = new();

    private IEventLoopGroup? _bossGroup;
    private IEventLoopGroup? _workerGroup;
    private IChannel? _serverChannel;

    public string Name { get; } = name;

    public int Port { get; } = port;

    public bool IsDatagram { get; } = datagram;

    public async Task StartAsync()
    {
        _workerGroup = new MultithreadEventLoopGroup();

        if (IsDatagram)
        {
            // UDP is connectionless: a single bound channel handles every sender, so the pipeline
            // is attached directly via Handler rather than ChildHandler (no accept/listen step).
            // There is no per-client connection to idle-timeout or track for graceful shutdown.
            var bootstrap = new Bootstrap()
                .Group(_workerGroup)
                .Channel<SocketDatagramChannel>()
                .Handler(new ActionChannelInitializer<IChannel>(channel => configurePipeline(channel.Pipeline)));

            _serverChannel = await bootstrap.BindAsync(IPAddress.Any, port);
        }
        else
        {
            _bossGroup = new MultithreadEventLoopGroup(1);

            var bootstrap = new ServerBootstrap()
                .Group(_bossGroup, _workerGroup)
                .Channel<TcpServerSocketChannel>()
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    var pipeline = channel.Pipeline;
                    pipeline.AddLast(new OpenChannelHandler(_channels));
                    if (timeoutSeconds > 0)
                    {
                        pipeline.AddLast(new IdleStateHandler(timeoutSeconds, 0, 0));
                        pipeline.AddLast(new IdleDisconnectHandler());
                    }
                    configurePipeline(pipeline);
                }));

            _serverChannel = await bootstrap.BindAsync(IPAddress.Any, port);
        }

        logger.LogInformation(
            "Started {Protocol} protocol on port {Port} ({Transport})", name, port, IsDatagram ? "UDP" : "TCP");
    }

    public async Task StopAsync()
    {
        if (_serverChannel != null)
        {
            await _serverChannel.CloseAsync();
        }
        await Task.WhenAll(_channels.Keys.Select(channel => channel.CloseAsync()));
        await Task.WhenAll(
            _bossGroup?.ShutdownGracefullyAsync() ?? Task.CompletedTask,
            _workerGroup?.ShutdownGracefullyAsync() ?? Task.CompletedTask);
    }
}
