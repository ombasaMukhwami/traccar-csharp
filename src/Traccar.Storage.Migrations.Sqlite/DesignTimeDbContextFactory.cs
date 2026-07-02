using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Traccar.Storage;

namespace Traccar.Storage.Migrations.Sqlite;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TraccarDbContext>()
            .UseSqlite(
                "Data Source=traccar.db",
                o => o.MigrationsAssembly("Traccar.Storage.Migrations.Sqlite"))
            .Options;
        return new TraccarDbContext(options);
    }
}
