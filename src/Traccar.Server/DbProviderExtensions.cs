using Microsoft.EntityFrameworkCore;
using Traccar.Storage;

namespace Traccar.Server;

/// <summary>
/// Maps the Database:Provider config value to the correct EF Core provider call.
/// Supported values (case-insensitive): sqlite, postgresql, postgres, mysql, mariadb, sqlserver, mssql.
/// Each provider routes migrations to its own dedicated assembly so migrations use
/// native types for that provider (no cross-provider type conversion patches needed).
/// </summary>
public static class DbProviderExtensions
{
    public static DbContextOptionsBuilder UseProvider(
        DbContextOptionsBuilder options,
        string provider,
        string connectionString,
        DatabaseRetryOptions? retry = null)
    {
        retry ??= new DatabaseRetryOptions();
        var delay = TimeSpan.FromSeconds(retry.MaxRetryDelaySeconds);

        return provider switch
        {
            "postgresql" or "postgres" => options.UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly("Traccar.Storage.Migrations.PostgreSQL");
                if (retry.Enable)
                    o.EnableRetryOnFailure(
                        maxRetryCount: retry.MaxRetryCount,
                        maxRetryDelay: delay,
                        errorCodesToAdd: null);
                if (retry.CommandTimeoutSeconds.HasValue)
                    o.CommandTimeout(retry.CommandTimeoutSeconds.Value);
            }),

            "mysql" or "mariadb" => options.UseMySQL(connectionString, o =>
            {
                o.MigrationsAssembly("Traccar.Storage.Migrations.MySQL");
                if (retry.Enable)
                    o.EnableRetryOnFailure(
                        maxRetryCount: retry.MaxRetryCount,
                        maxRetryDelay: delay,
                        errorNumbersToAdd: null);
                if (retry.CommandTimeoutSeconds.HasValue)
                    o.CommandTimeout(retry.CommandTimeoutSeconds.Value);
            }),

            "sqlserver" or "mssql" => options.UseSqlServer(connectionString, o =>
            {
                o.MigrationsAssembly("Traccar.Storage.Migrations.SqlServer");
                if (retry.Enable)
                    o.EnableRetryOnFailure(
                        maxRetryCount: retry.MaxRetryCount,
                        maxRetryDelay: delay,
                        errorNumbersToAdd: null);
                if (retry.CommandTimeoutSeconds.HasValue)
                    o.CommandTimeout(retry.CommandTimeoutSeconds.Value);
            }),

            _ => options.UseSqlite(connectionString, o =>   // sqlite (default)
            {
                o.MigrationsAssembly("Traccar.Storage.Migrations.Sqlite");
                // SQLite is file-based; EnableRetryOnFailure is not available on SqliteDbContextOptionsBuilder.
                if (retry.CommandTimeoutSeconds.HasValue)
                    o.CommandTimeout(retry.CommandTimeoutSeconds.Value);
            }),
        };
    }
}
