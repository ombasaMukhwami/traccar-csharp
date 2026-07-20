using System.Threading.Tasks.Sources;
using DotNetty.Transport.Channels;

namespace Traccar.Protocols.Handlers;

/// <summary>
/// Holds outbound protocol responses until all inbound-decoded objects (positions) for the same
/// received frame have been fully processed (saved to DB), then flushes them. This mirrors Java's
/// AcknowledgementHandler which prevents an ACK from reaching the device before Traccar has
/// finished persisting the positions that ACK covers.
///
/// Usage (sent via ctx.WriteAsync as marker objects, not written to the wire):
///   EventReceived  — decoder signals a raw frame arrived; queue outbound messages from this point.
///   EventDecoded   — decoder signals which objects it decoded from the frame; track them.
///   EventHandled   — persistence layer signals one object is done; when all done, flush the queue.
/// </summary>
public sealed class AcknowledgementHandler : ChannelHandlerAdapter
{
    public interface IEvent { }
    public sealed class EventReceived : IEvent { }

    public sealed class EventDecoded(IReadOnlyCollection<object> objects) : IEvent
    {
        public IReadOnlyCollection<object> Objects { get; } = objects;
    }

    public sealed class EventHandled(object obj) : IEvent
    {
        public object Object { get; } = obj;
    }

    private sealed record Entry(object Message, TaskCompletionSource Completion);

    private readonly object _lock = new();
    private List<Entry>? _queue;
    private readonly HashSet<object> _waiting = [];

    public override Task WriteAsync(IChannelHandlerContext context, object message)
    {
        List<Entry>? toFlush = null;
        TaskCompletionSource? tcs = null;

        lock (_lock)
        {
            if (message is IEvent evt)
            {
                switch (evt)
                {
                    case EventReceived:
                        _queue ??= [];
                        break;
                    case EventDecoded decoded:
                        foreach (var obj in decoded.Objects) _waiting.Add(obj);
                        break;
                    case EventHandled handled:
                        _waiting.Remove(handled.Object);
                        break;
                }

                // Flush once every decoded object has been handled (and queue was started).
                if (message is not EventReceived && _waiting.Count == 0 && _queue != null)
                {
                    toFlush = _queue;
                    _queue = null;
                }
            }
            else if (_queue != null)
            {
                tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _queue.Add(new Entry(message, tcs));
            }
        }

        if (toFlush != null)
        {
            foreach (var entry in toFlush)
            {
                _ = context.WriteAsync(entry.Message)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully) entry.Completion.TrySetResult();
                        else entry.Completion.TrySetException(t.Exception ?? new Exception("Write failed"));
                    }, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        return tcs?.Task ?? Task.CompletedTask;
    }
}
