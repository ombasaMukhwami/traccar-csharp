using Microsoft.Extensions.Configuration;
using NCalc;
using Traccar.Model;

namespace Traccar.Protocols.Handlers;

/// <summary>
/// Evaluates NCalc expressions against a position context. Mirrors Java's ComputedAttributesProvider
/// which uses JEXL3 — NCalc covers the common subset of arithmetic, comparison, and logical expressions
/// used in practice (e.g. "speed * 1.852", "if(ignition, hours + elapsed, hours)", "odometer / 1000").
/// </summary>
public sealed class ComputedAttributesProvider(IConfiguration configuration)
{
    private readonly bool _includeLastAttributes =
        configuration.GetValue(ConfigKeys.Processing.ComputedAttributesLastAttributes, false);

    public object? Compute(DeviceAttribute attribute, Position position, Position? last)
    {
        var expr = new Expression(attribute.Expression ?? string.Empty);
        PrepareParameters(expr, position, last);
        return expr.Evaluate();
    }

    private void PrepareParameters(Expression expr, Position position, Position? last)
    {
        // Core position fields
        expr.Parameters["speed"] = position.Speed;
        expr.Parameters["latitude"] = position.Latitude;
        expr.Parameters["longitude"] = position.Longitude;
        expr.Parameters["altitude"] = position.Altitude;
        expr.Parameters["accuracy"] = position.Accuracy;
        expr.Parameters["course"] = position.Course;
        expr.Parameters["valid"] = position.Valid;
        expr.Parameters["outdated"] = position.Outdated;
        expr.Parameters["fixTime"] = position.FixTime;
        expr.Parameters["deviceTime"] = position.DeviceTime;
        expr.Parameters["serverTime"] = (object) position.ServerTime;

        // All position attributes available as top-level variables
        foreach (var (key, value) in position.Attributes)
            expr.Parameters[key] = value;

        // Optional last-position variables with "last" prefix (e.g. "lastSpeed", "lastOdometer")
        if (_includeLastAttributes && last != null)
        {
            expr.Parameters["lastSpeed"] = last.Speed;
            expr.Parameters["lastLatitude"] = last.Latitude;
            expr.Parameters["lastLongitude"] = last.Longitude;
            foreach (var (key, value) in last.Attributes)
                expr.Parameters["last" + char.ToUpperInvariant(key[0]) + key[1..]] = value;
        }
    }
}
