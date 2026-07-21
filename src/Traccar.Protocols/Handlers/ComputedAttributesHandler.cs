using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Handlers;

/// <summary>
/// Evaluates device-level scripted attributes and writes their results back onto the position.
/// Runs in two phases that mirror Java's ComputedAttributesHandler.Early/Late split:
///   Early (priority &lt; 0)  — before DistanceHandler/EngineHoursHandler so expressions can
///                             adjust raw coordinates/speed before other enrichment reads them.
///   Late  (priority ≥ 0)  — after all other enrichment, just before FilterHandler.
/// </summary>
public sealed class ComputedAttributesHandler(
    DeviceAttributeCache cache,
    ComputedAttributesProvider provider,
    bool early,
    ILogger<ComputedAttributesHandler> logger)
{
    public void Process(Position position, Position? last)
    {
        var attributes = cache.Get(position.DeviceId);

        var filtered = attributes
            .Where(a => (a.Priority < 0) == early)
            .OrderByDescending(a => a.Priority);

        foreach (var attr in filtered)
        {
            if (attr.Attribute == null) continue;
            try
            {
                var result = provider.Compute(attr, position, last);
                if (result != null)
                    Apply(position, attr, result);
                else
                    position.Remove(attr.Attribute);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Computed attribute '{Attribute}' evaluation error", attr.Attribute);
            }
        }
    }

    private static void Apply(Position position, DeviceAttribute attr, object result)
    {
        switch (attr.Attribute)
        {
            case "valid":    position.Valid    = Convert.ToBoolean(result); break;
            case "latitude": position.Latitude = Convert.ToDouble(result);  break;
            case "longitude":position.Longitude= Convert.ToDouble(result);  break;
            case "altitude": position.Altitude = Convert.ToDouble(result);  break;
            case "speed":    position.Speed    = Convert.ToDouble(result);  break;
            case "course":   position.Course   = Convert.ToDouble(result);  break;
            case "address":  position.Address  = result.ToString();         break;
            case "accuracy": position.Accuracy = Convert.ToDouble(result);  break;
            default:
                position.Attributes[attr.Attribute!] = attr.Type switch
                {
                    "number"  => (object) Convert.ToDouble(result),
                    "boolean" => Convert.ToBoolean(result),
                    _         => result.ToString()!,
                };
                break;
        }
    }
}
