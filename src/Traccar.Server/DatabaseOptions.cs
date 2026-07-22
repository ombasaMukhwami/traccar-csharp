using Microsoft.Extensions.Configuration;

namespace Traccar.Server;

/// <summary>
/// PostgreSQL connection settings — the connection string (from ConnectionStrings:DefaultConnection)
/// plus the "Database:Retry" transient-fault settings, combined into one bindable object so
/// Program.cs and the design-time factory (<see cref="TraccarDbContextFactory"/>) read
/// appsettings.json through a single path instead of duplicating fallback logic.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "Host=localhost;Database=traccar";

    public DatabaseRetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Caps how many DbContext instances (each ~1 physical Postgres connection) the app will
    /// have open at once — enforced client-side via a semaphore in ThrottledDbContextFactory, so
    /// a burst of device traffic queues in the app instead of exceeding Postgres's own
    /// max_connections. Keep meaningfully below max_connections (Postgres default 100): once
    /// max_connections is hit, EF's retry-on-failure policy can turn a transient spike into a
    /// self-sustaining retry storm, since every queued retry re-contends for the same scarce
    /// slots instead of waiting in an orderly queue.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 80;

    public static DatabaseOptions Bind(IConfiguration configuration)
    {
        var options = new DatabaseOptions();
        configuration.GetSection(SectionName).Bind(options);
        options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? options.ConnectionString;
        return options;
    }
}
