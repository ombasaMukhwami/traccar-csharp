namespace Traccar.Protocols.Forward;

/// <summary>Mirrors Java's forward.PositionForwarder.</summary>
public interface IPositionForwarder
{
    void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler);
}
