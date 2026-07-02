using Microsoft.EntityFrameworkCore;
using Traccar.Storage;

namespace Traccar.Server;

/// <summary>
/// Maps the Database:Provider config value to the correct EF Core provider call.
/// Supported values (case-insensitive): sqlite, postgresql, postgres, mysql, mariadb, sqlserver, mssql.
/// </summary>
public static class DbProviderExtensions
{
    public static DbContextOptionsBuilder UseProvider(
        DbContextOptionsBuilder options, string provider, string connectionString) =>
        provider switch
        {
            "postgresql" or "postgres" =>
                options.UseNpgsql(connectionString, o => o.MigrationsAssembly("Traccar.Storage")),

            "mysql" or "mariadb" =>
                options.UseMySQL(connectionString, o => o.MigrationsAssembly("Traccar.Storage")),

            "sqlserver" or "mssql" =>
                options.UseSqlServer(connectionString, o => o.MigrationsAssembly("Traccar.Storage")),

            _ => // sqlite (default)
                options.UseSqlite(connectionString, o => o.MigrationsAssembly("Traccar.Storage")),
        };
}
