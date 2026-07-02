using System.Threading;
using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Storage;

namespace Traccar.Protocols;

/// <summary>Mirrors Java's handler.PositionForwardingHandler, including its retry/backoff behavior.</summary>
public sealed class PositionForwardingHandler(
    IPositionForwarder? positionForwarder, IDbContextFactory<TraccarDbContext> dbContextFactory,
    IConfiguration configuration, ILogger<PositionForwardingHandler> logger) : ChannelHandlerAdapter
{
    private readonly bool _retryEnabled = configuration.GetValue(ConfigKeys.Forward.Retry.Enable, false);
    private readonly int _retryDelay = configuration.GetValue(ConfigKeys.Forward.Retry.Delay, 100);
    private readonly int _retryCount = configuration.GetValue(ConfigKeys.Forward.Retry.Count, 10);
    private readonly int _retryLimit = configuration.GetValue(ConfigKeys.Forward.Retry.Limit, 100);

    private int _deliveryPending;

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        if (positionForwarder != null && message is Position position)
        {
            _ = ForwardAsync(position);
        }
        context.FireChannelRead(message);
    }

    private async Task ForwardAsync(Position position)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var device = await db.Devices.FindAsync(position.DeviceId);

        Interlocked.Increment(ref _deliveryPending);
        Send(new PositionForwardData(position, device), retries: 0);
    }

    private void Send(PositionForwardData data, int retries)
    {
        positionForwarder!.Forward(data, (success, throwable) =>
        {
            if (success)
            {
                Interlocked.Decrement(ref _deliveryPending);
            }
            else
            {
                Retry(data, retries, throwable);
            }
        });
    }

    private void Retry(PositionForwardData data, int retries, Exception? throwable)
    {
        var scheduled = false;
        if (_retryEnabled && _deliveryPending <= _retryLimit && retries < _retryCount)
        {
            Schedule(data, retries);
            scheduled = true;
        }

        var pending = scheduled ? _deliveryPending : Interlocked.Decrement(ref _deliveryPending);
        logger.LogWarning(throwable, "Position forwarding failed: {Pending} pending", pending);
    }

    private void Schedule(PositionForwardData data, int retries)
    {
        var delay = _retryDelay * (1L << retries);
        _ = Task.Delay(TimeSpan.FromMilliseconds(delay)).ContinueWith(_ => Send(data, retries + 1));
    }
}
