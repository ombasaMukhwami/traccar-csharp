using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Traccar.Storage;

namespace Traccar.Server;

/// <summary>
/// Configures the PostgreSQL EF Core provider, routing migrations to the dedicated
/// Traccar.Storage.Migrations.PostgreSQL assembly.
/// </summary>
public static class DbProviderExtensions
{
    public static DbContextOptionsBuilder UseProvider(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var database = DatabaseOptions.Bind(configuration);
        var retry = database.Retry;
        var delay = TimeSpan.FromSeconds(retry.MaxRetryDelaySeconds);

        return options.UseNpgsql(database.ConnectionString, o =>
        {
            o.MigrationsAssembly("Traccar.Storage.Migrations.PostgreSQL");
            if (retry.Enable)
            {
                o.EnableRetryOnFailure(
                    maxRetryCount: retry.MaxRetryCount,
                    maxRetryDelay: delay,
                    errorCodesToAdd: null);
            }
            if (retry.CommandTimeoutSeconds.HasValue)
            {
                o.CommandTimeout(retry.CommandTimeoutSeconds.Value);
            }
        }).UseSnakeCaseNamingConvention();
    }
}
