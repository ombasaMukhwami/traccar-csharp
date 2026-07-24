using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols;

/// <summary>
/// A plain class rather than a DotNetty pipeline handler — see BaseProtocol.AddPositionServer's
/// ContinuePosition, which calls <see cref="Process"/> directly. Persistence has to stay independent
/// of the producing channel's lifecycle: it can run from a background thread pool thread after
/// GeocoderHandler's async lookup completes, well after that channel may have closed (see
/// GeocoderHandler's own doc comment for the incident this fixes).
/// </summary>
public sealed class PositionPersistHandler(
    IDbContextFactory<TraccarDbContext> dbContextFactory,
    PositionCache positionCache,
    ILogger<PositionPersistHandler> logger)
{
    public void Process(Position position, Action<Position> onSaved)
    {
        try
        {
            using var db = dbContextFactory.CreateDbContext();
            db.Positions.Add(position);
            db.SaveChanges();

            // Only overwrite the device's current position when this fix is not older than the
            // cached last fix — prevents archival replay from clobbering newer live positions.
            var last = positionCache.GetLastPosition(position.DeviceId);
            bool isLatest = last == null ||
                position.FixTime.GetValueOrDefault() >= last.FixTime.GetValueOrDefault();

            if (isLatest)
            {
                var device = db.Devices.Find(position.DeviceId);
                if (device != null)
                {
                    device.PositionId = position.Id;
                    device.LastUpdate = position.DeviceTime ?? position.ServerTime;
                    db.SaveChanges();
                }
            }

            // Continue while the cache still holds the previous position so that
            // EventProcessingHandler sees the old last for duplicate-alarm / proximity checks.
            onSaved(position);

            if (isLatest)
                positionCache.UpdatePosition(position);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to save position for device {DeviceId}", position.DeviceId);
        }
    }
}
