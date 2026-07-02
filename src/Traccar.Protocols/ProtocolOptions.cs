namespace Traccar.Protocols;

/// <summary>
/// Per-protocol configuration bound from the "Protocols:{name}" config section.
/// Registered as named IOptions so each protocol reads its own section by name.
/// </summary>
public sealed class ProtocolOptions
{
    public const int DefaultTimeout = 1800;

    /// <summary>Protocol name (e.g. "gt06"). Set automatically by PostConfigure — not required in appsettings.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>TCP/UDP port the protocol listens on. Required — protocols without a positive port are not started.</summary>
    public int Port { get; set; }

    /// <summary>Idle-disconnect timeout in seconds. Defaults to 1800 (30 min), matching Java's server.timeout default.</summary>
    public int Timeout { get; set; } = DefaultTimeout;
}
