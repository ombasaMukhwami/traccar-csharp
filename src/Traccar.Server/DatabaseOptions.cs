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

    public static DatabaseOptions Bind(IConfiguration configuration)
    {
        var options = new DatabaseOptions();
        configuration.GetSection(SectionName).Bind(options);
        options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? options.ConnectionString;
        return options;
    }
}
