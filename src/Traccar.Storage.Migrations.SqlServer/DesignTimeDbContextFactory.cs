using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Traccar.Storage;

namespace Traccar.Storage.Migrations.SqlServer;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TraccarDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=traccar;Trusted_Connection=True;TrustServerCertificate=True",
                o => o.MigrationsAssembly("Traccar.Storage.Migrations.SqlServer"))
            .Options;
        return new TraccarDbContext(options);
    }
}
