using Microsoft.EntityFrameworkCore;
using Traccar.Model;

namespace Traccar.Storage;

public class TraccarDbContext(DbContextOptions<TraccarDbContext> options) : DbContext(options)
{
    private Action? _onDisposed;

    /// <summary>Set by ThrottledDbContextFactory so releasing this context's connection-pool
    /// slot happens automatically on Dispose/DisposeAsync, regardless of which one the caller
    /// uses (both "await using" and plain "using" are common across this codebase).</summary>
    internal void SetDisposalCallback(Action callback) => _onDisposed = callback;

    public override void Dispose()
    {
        var callback = Interlocked.Exchange(ref _onDisposed, null);
        base.Dispose();
        callback?.Invoke();
    }

    public override async ValueTask DisposeAsync()
    {
        var callback = Interlocked.Exchange(ref _onDisposed, null);
        await base.DisposeAsync();
        callback?.Invoke();
    }

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<Command> Commands => Set<Command>();

    public DbSet<DeviceAttribute> DeviceAttributes => Set<DeviceAttribute>();

    public DbSet<EventType> EventTypes => Set<EventType>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<AgentDetails> Agents => Set<AgentDetails>();

    public DbSet<SimCard> SimCards => Set<SimCard>();

    public DbSet<RouteInfo> Routes => Set<RouteInfo>();

    public DbSet<DeviceModelInfo> DeviceModels => Set<DeviceModelInfo>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Geozone> Geozones => Set<Geozone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.UniqueId).IsUnique();

            // Every device must belong to a real client — Restrict (not Cascade) so deleting a
            // client with devices still on it fails loudly instead of silently wiping them;
            // move or delete the devices first.
            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // A device's "last position" pointer — nullable (a brand-new device has none yet),
            // so this can't be Cascade the way Position.DeviceId is below (that would delete the
            // device itself the moment its last position was deleted). SetNull instead: deleting
            // that position just clears the pointer.
            entity.HasOne<Position>()
                .WithMany()
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.Property(e => e.RouteAccess)
                .HasConversion(JsonValueConverter<List<RouteAccessGrant>>.Converter);
            entity.Property(e => e.ClientId)
                .HasConversion(JsonValueConverter<List<int>>.Converter);
            entity.Property(e => e.UserType)
                .HasConversion<string>();
            entity.Ignore(e => e.CurrentPassword);
            entity.Ignore(e => e.NewPassword);
            entity.Ignore(e => e.ConfirmPassword);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.Property(e => e.Network)
                .HasConversion(JsonValueConverter<Network>.Converter);
            entity.Property(e => e.GeofenceIds)
                .HasConversion(JsonValueConverter<List<long>>.Converter);
            entity.HasIndex(e => new { e.DeviceId, e.FixTime });

            // Cascade: a position only ever exists for its owning device, so deleting the device
            // (or truncating both tables together) should take its positions with it.
            entity.HasOne<Device>()
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => new { e.DeviceId, e.EventTime });

            entity.HasOne<Device>()
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deliberately no FK on PositionId: deviceOnline/deviceOffline events (see
            // ConnectionManager.UpdateDeviceStatus) aren't tied to a position and use 0 as a
            // "no position" sentinel — a non-nullable FK would reject those inserts outright.
        });

        modelBuilder.Entity<Command>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.DeviceId);

            entity.HasOne<Device>()
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceAttribute>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.DeviceId);

            entity.HasOne<Device>()
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(256);
            entity.HasData(EventType.All);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(e => e.DeviceLimit).HasDefaultValue(110);
        });

        modelBuilder.Entity<RouteInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasData(RouteInfo.Catalog);
        });

        modelBuilder.Entity<DeviceModelInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasData(DeviceModelInfo.Catalog);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Geozone>(entity =>
        {
            entity.Property(e => e.Data)
                .HasConversion(JsonValueConverter<GeozoneShape>.Converter);
            entity.HasIndex(e => e.ClientId);
        });
    }
}
