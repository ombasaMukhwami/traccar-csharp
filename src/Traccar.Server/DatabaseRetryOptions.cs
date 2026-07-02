namespace Traccar.Server;

/// <summary>
/// Configures EF Core's built-in transient-fault retry strategy for the active database provider.
/// Bound from the "Database:Retry" section in appsettings.json.
/// </summary>
public sealed class DatabaseRetryOptions
{
    /// <summary>Whether the retry strategy is active. Default: true.</summary>
    public bool Enable { get; set; } = true;

    /// <summary>Maximum number of retry attempts before the operation fails. Default: 6.</summary>
    public int MaxRetryCount { get; set; } = 6;

    /// <summary>Maximum delay between retries in seconds. Default: 30.</summary>
    public int MaxRetryDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Per-command timeout in seconds. Null uses the provider default (typically 30 s).
    /// </summary>
    public int? CommandTimeoutSeconds { get; set; }
}
