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
    private readonly ConcurrentDictionary<IChannel, byte> channels = new();

    private IEventLoopGroup? bossGroup;
    private IEventLoopGroup? workerGroup;
    private IChannel? serverChannel;

    public string Name { get; } = name;

    public int Port { get; } = port;

    public bool IsDatagram { get; } = datagram;

    public async Task StartAsync()
    {
        workerGroup = new MultithreadEventLoopGroup();

        if (IsDatagram)
        {
            // UDP is connectionless: a single bound channel handles every sender, so the pipeline
            // is attached directly via Handler rather than ChildHandler (no accept/listen step).
            // There is no per-client connection to idle-timeout or track for graceful shutdown.
            var bootstrap = new Bootstrap()
                .Group(workerGroup)
                .Channel<SocketDatagramChannel>()
                .Handler(new ActionChannelInitializer<IChannel>(channel => configurePipeline(channel.Pipeline)));

            serverChannel = await bootstrap.BindAsync(IPAddress.Any, port);
        }
        else
        {
            bossGroup = new MultithreadEventLoopGroup(1);

            var bootstrap = new ServerBootstrap()
                .Group(bossGroup, workerGroup)
                .Channel<TcpServerSocketChannel>()
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    var pipeline = channel.Pipeline;
                    pipeline.AddLast(new OpenChannelHandler(channels));
                    if (timeoutSeconds > 0)
                    {
                        pipeline.AddLast(new IdleStateHandler(timeoutSeconds, 0, 0));
                        pipeline.AddLast(new IdleDisconnectHandler());
                    }
                    configurePipeline(pipeline);
                }));

            serverChannel = await bootstrap.BindAsync(IPAddress.Any, port);
        }

        logger.LogInformation(
            "Started {Protocol} protocol on port {Port} ({Transport})", name, port, IsDatagram ? "UDP" : "TCP");
    }

    public async Task StopAsync()
    {
        if (serverChannel != null)
        {
            await serverChannel.CloseAsync();
        }
        await Task.WhenAll(channels.Keys.Select(channel => channel.CloseAsync()));
        await Task.WhenAll(
            bossGroup?.ShutdownGracefullyAsync() ?? Task.CompletedTask,
            workerGroup?.ShutdownGracefullyAsync() ?? Task.CompletedTask);
    }
}
