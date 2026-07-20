using Microsoft.Extensions.Configuration;
using Traccar.Model;

namespace Traccar.Protocols.Handlers;

public sealed class MotionHandler(IConfiguration configuration)
{
    private readonly double _threshold =
        configuration.GetValue(ConfigKeys.Events.Motion.SpeedThreshold, 0.01);

    public void Process(Position position)
    {
        if (!position.HasAttribute(Position.KeyMotion))
        {
            position.Set(Position.KeyMotion, position.Speed > _threshold);
        }
    }
}
