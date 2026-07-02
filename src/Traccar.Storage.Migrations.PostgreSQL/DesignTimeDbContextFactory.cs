using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Traccar.Storage;

namespace Traccar.Storage.Migrations.PostgreSQL;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TraccarDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=traccar",
                o => o.MigrationsAssembly("Traccar.Storage.Migrations.PostgreSQL"))
            .Options;
        return new TraccarDbContext(options);
    }
}
