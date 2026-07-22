using Microsoft.EntityFrameworkCore;

namespace Traccar.Storage;

/// <summary>
/// Bounds how many TraccarDbContext instances (each ~1 physical Postgres connection) can be open
/// at once, via a semaphore acquired before construction and released on Dispose/DisposeAsync
/// (see TraccarDbContext.SetDisposalCallback). Once the limit is hit, callers queue in-process
/// instead of opening a connection Postgres has to reject.
///
/// This exists because of a real failure mode found under load testing: with nothing capping
/// concurrency, a burst of device traffic drove concurrent connection attempts well past
/// Postgres's max_connections, and EF's retry-on-failure policy turned that into a
/// self-sustaining retry storm — every queued retry re-contended for the same scarce slots, so
/// the error rate never drained even after the offending load stopped. Queueing client-side,
/// below max_connections, avoids ever putting Postgres in that position.
/// </summary>
public sealed class ThrottledDbContextFactory(
    DbContextOptions<TraccarDbContext> options, SemaphoreSlim semaphore) : IDbContextFactory<TraccarDbContext>
{
    public TraccarDbContext CreateDbContext()
    {
        semaphore.Wait();
        try
        {
            var context = new TraccarDbContext(options);
            context.SetDisposalCallback(() => semaphore.Release());
            return context;
        }
        catch
        {
            semaphore.Release();
            throw;
        }
    }
}
