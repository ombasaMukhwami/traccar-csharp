namespace Traccar.Protocols;

/// <summary>Mirrors Java's org.traccar.LifecycleObject.</summary>
public interface ILifecycleObject
{
    Task StartAsync();

    Task StopAsync();
}
