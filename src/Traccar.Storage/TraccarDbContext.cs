using Microsoft.EntityFrameworkCore;
using Traccar.Model;

namespace Traccar.Storage;

public class TraccarDbContext(DbContextOptions<TraccarDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<Command> Commands => Set<Command>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("tc_devices");
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.UniqueId).IsUnique();
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.ToTable("tc_groups");
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("tc_users");
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.ToTable("tc_positions");
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.Property(e => e.Network)
                .HasConversion(JsonValueConverter<Network>.Converter);
            entity.Property(e => e.GeofenceIds)
                .HasConversion(JsonValueConverter<List<long>>.Converter);
            entity.HasIndex(e => new { e.DeviceId, e.FixTime });
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("tc_events");
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => new { e.DeviceId, e.EventTime });
        });

        modelBuilder.Entity<Command>(entity =>
        {
            entity.ToTable("tc_commands");
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.DeviceId);
        });
    }
}
