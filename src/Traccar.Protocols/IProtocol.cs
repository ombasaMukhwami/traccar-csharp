namespace Traccar.Protocols;

/// <summary>
/// Mirrors Java's org.traccar.Protocol. Text-command (SMS) sending is not part of this port (no
/// SmsManager equivalent exists), so getSupportedTextCommands/sendTextCommand are not included.
/// </summary>
public interface IProtocol
{
    string Name { get; }

    IReadOnlyCollection<ITrackerConnector> ConnectorList { get; }

    IReadOnlyCollection<string> SupportedDataCommands { get; }
}
