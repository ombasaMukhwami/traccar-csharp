using System.Collections.Concurrent;
using Traccar.Model;

namespace Traccar.Protocols;

public sealed class PositionCache
{
    private readonly ConcurrentDictionary<long, Position> _positions = new();

    public Position? GetLastPosition(long deviceId)
        => _positions.GetValueOrDefault(deviceId);

    public void UpdatePosition(Position position)
        => _positions[position.DeviceId] = position;
}
