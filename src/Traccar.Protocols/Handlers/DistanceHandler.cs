using Microsoft.Extensions.Configuration;
using Traccar.Model;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Handlers;

public sealed class DistanceHandler(IConfiguration configuration)
{
    private readonly bool _filter = configuration.GetValue(ConfigKeys.Coordinates.Filter, false);
    private readonly int _minError = configuration.GetValue(ConfigKeys.Coordinates.MinError, 0);
    private readonly int _maxError = configuration.GetValue(ConfigKeys.Coordinates.MaxError, 0);

    public void Process(Position position, Position? last)
    {
        double distance = position.HasAttribute(Position.KeyDistance)
            ? position.GetDouble(Position.KeyDistance)
            : 0.0;

        double totalDistance;
        if (last != null)
        {
            totalDistance = last.GetDouble(Position.KeyTotalDistance);
            if (!position.HasAttribute(Position.KeyDistance))
                distance = DistanceCalculator.Distance(
                    position.Latitude, position.Longitude, last.Latitude, last.Longitude);

            if (_filter && last.Latitude != 0 && last.Longitude != 0)
            {
                bool satisfiesMin = _minError == 0 || distance > _minError;
                bool satisfiesMax = _maxError == 0 || distance < _maxError;
                if (!satisfiesMin || !satisfiesMax)
                {
                    position.Valid = last.Valid;
                    position.Latitude = last.Latitude;
                    position.Longitude = last.Longitude;
                    distance = 0;
                }
            }
        }
        else
        {
            totalDistance = 0.0;
        }

        position.Set(Position.KeyDistance, distance);
        position.Set(Position.KeyTotalDistance, totalDistance + distance);
    }
}
