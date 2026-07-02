using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Traccar.Protocols;
using Traccar.Storage;

namespace Traccar.Server;

/// <summary>
/// Design-time factory used by "dotnet ef migrations" tooling.
/// Reads Database:Provider and ConnectionStrings:DefaultConnection from appsettings.json
/// so migrations are generated for the currently configured database.
/// </summary>
public class TraccarDbContextFactory : IDesignTimeDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Data Source=traccar.db";

        var provider = (config[ConfigKeys.Database.Provider] ?? "sqlite").ToLowerInvariant();

        var optionsBuilder = new DbContextOptionsBuilder<TraccarDbContext>();
        DbProviderExtensions.UseProvider(optionsBuilder, provider, connectionString);

        return new TraccarDbContext(optionsBuilder.Options);
    }
}
