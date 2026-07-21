using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Traccar.Storage;

namespace Traccar.Server;

/// <summary>
/// Design-time factory used by "dotnet ef migrations" tooling.
/// Reads ConnectionStrings:DefaultConnection from appsettings.json so migrations are
/// generated against the configured PostgreSQL database.
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

        var optionsBuilder = new DbContextOptionsBuilder<TraccarDbContext>();
        DbProviderExtensions.UseProvider(optionsBuilder, config);

        return new TraccarDbContext(optionsBuilder.Options);
    }
}
