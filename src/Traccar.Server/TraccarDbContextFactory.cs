using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Traccar.Storage;

namespace Traccar.Server;

public class TraccarDbContextFactory : IDesignTimeDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TraccarDbContext>();
        optionsBuilder.UseSqlite("Data Source=traccar.db");
        return new TraccarDbContext(optionsBuilder.Options);
    }
}
