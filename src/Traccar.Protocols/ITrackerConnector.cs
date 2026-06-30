namespace Traccar.Protocols;

/// <summary>Mirrors Java's org.traccar.TrackerConnector.</summary>
public interface ITrackerConnector : ILifecycleObject
{
    bool IsDatagram { get; }
}
