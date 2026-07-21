namespace Traccar.Protocols.Forward;

/// <summary>
/// Fans a single forward-call out to multiple <see cref="IPositionForwarder"/>s — lets the
/// optional external broker (Kafka/RabbitMQ/SignalR-client, selected by Forward:Type) and a
/// locally-hosted notifier (e.g. Traccar.Server's own SignalR hub) run side by side, since
/// <see cref="PositionForwardingHandler"/> only holds a single forwarder slot.
/// </summary>
public sealed class CompositePositionForwarder(IReadOnlyList<IPositionForwarder> forwarders) : IPositionForwarder
{
    public void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler)
    {
        if (forwarders.Count == 0)
        {
            resultHandler(true, null);
            return;
        }

        var remaining = forwarders.Count;
        var success = true;
        Exception? firstError = null;
        var sync = new object();

        foreach (var forwarder in forwarders)
        {
            forwarder.Forward(data, (forwarderSuccess, error) =>
            {
                lock (sync)
                {
                    if (!forwarderSuccess)
                    {
                        success = false;
                        firstError ??= error;
                    }
                    if (--remaining == 0)
                    {
                        resultHandler(success, firstError);
                    }
                }
            });
        }
    }
}
