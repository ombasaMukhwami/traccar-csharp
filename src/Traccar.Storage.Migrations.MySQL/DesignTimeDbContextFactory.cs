using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Traccar.Storage;

namespace Traccar.Storage.Migrations.MySQL;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TraccarDbContext>()
            .UseMySQL(
                "Server=localhost;Database=traccar",
                o => o.MigrationsAssembly("Traccar.Storage.Migrations.MySQL"))
            .Options;
        return new TraccarDbContext(options);
    }
}
