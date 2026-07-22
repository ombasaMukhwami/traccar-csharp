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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.UniqueId).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.Property(e => e.RouteAccess)
                .HasConversion(JsonValueConverter<List<RouteAccessGrant>>.Converter);
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
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => new { e.DeviceId, e.EventTime });
        });

        modelBuilder.Entity<Command>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.DeviceId);
        });

        modelBuilder.Entity<DeviceAttribute>(entity =>
        {
            entity.Property(e => e.Attributes)
                .HasConversion(AttributesConverter.Converter, AttributesConverter.Comparer);
            entity.HasIndex(e => e.DeviceId);
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
        });
    }
}
