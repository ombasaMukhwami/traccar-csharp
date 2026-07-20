using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;

namespace Traccar.Protocols.Handlers;

public sealed class FilterHandler(IConfiguration configuration, ILogger<FilterHandler> logger)
{
    private readonly bool _filterInvalid = configuration.GetValue(ConfigKeys.Filter.Invalid, false);
    private readonly bool _filterZero = configuration.GetValue(ConfigKeys.Filter.Zero, false);
    private readonly bool _filterDuplicate = configuration.GetValue(ConfigKeys.Filter.Duplicate, false);
    private readonly bool _filterOutdated = configuration.GetValue(ConfigKeys.Filter.Outdated, false);
    private readonly long? _filterFuture = configuration.GetValue<long?>(ConfigKeys.Filter.Future, null);
    private readonly long? _filterPast = configuration.GetValue<long?>(ConfigKeys.Filter.Past, null);
    private readonly int? _filterAccuracy = configuration.GetValue<int?>(ConfigKeys.Filter.Accuracy, null);
    private readonly bool _filterApproximate = configuration.GetValue(ConfigKeys.Filter.Approximate, false);
    private readonly bool _filterStatic = configuration.GetValue(ConfigKeys.Filter.Static, false);
    private readonly int? _filterDistance = configuration.GetValue<int?>(ConfigKeys.Filter.Distance, null);
    private readonly int? _filterMaxSpeed = configuration.GetValue<int?>(ConfigKeys.Filter.MaxSpeed, null);
    private readonly int? _filterMinPeriod = configuration.GetValue<int?>(ConfigKeys.Filter.MinPeriod, null);
    private readonly long? _skipLimit = configuration.GetValue<long?>(ConfigKeys.Filter.SkipLimit, null);
    private readonly string? _skipAttributes = configuration[ConfigKeys.Filter.SkipAttributes];

    // Returns true if position should be dropped.
    public bool Filter(Position position, Position? last)
    {
        var reasons = new List<string>(4);

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long fixMs = position.FixTime.HasValue
            ? new DateTimeOffset(position.FixTime.Value, TimeSpan.Zero).ToUnixTimeMilliseconds()
            : nowMs;

        // --- always-on validity filters ---
        if (_filterInvalid && (!position.Valid
                || position.Latitude > 90 || position.Longitude > 180
                || position.Latitude < -90 || position.Longitude < -180))
            reasons.Add("Invalid");

        if (_filterZero && position.Latitude == 0.0 && position.Longitude == 0.0)
            reasons.Add("Zero");

        if (_filterOutdated && position.Outdated)
            reasons.Add("Outdated");

        if (_filterFuture.HasValue && fixMs > nowMs + _filterFuture.Value * 1000)
            reasons.Add("Future");

        if (_filterPast.HasValue && fixMs < nowMs - _filterPast.Value * 1000)
            reasons.Add("Past");

        if (_filterAccuracy.HasValue && position.Accuracy > _filterAccuracy.Value)
            reasons.Add("Accuracy");

        if (_filterApproximate && position.GetBoolean(Position.KeyApproximate))
            reasons.Add("Approximate");

        // --- excessive-data filters (overrideable by skip-limit / skip-attributes) ---
        bool skip = CanSkip(position, last);

        if (_filterDuplicate && !skip && IsDuplicate(position, last))
            reasons.Add("Duplicate");

        if (_filterStatic && position.Speed == 0.0 && !skip)
            reasons.Add("Static");

        if (_filterDistance.HasValue && last != null && !skip
                && position.GetDouble(Position.KeyDistance) < _filterDistance.Value)
            reasons.Add("Distance");

        if (_filterMaxSpeed.HasValue && last != null)
        {
            double dist = position.GetDouble(Position.KeyDistance);
            long lastFixMs = last.FixTime.HasValue
                ? new DateTimeOffset(last.FixTime.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() : 0;
            double time = fixMs - lastFixMs;
            if (time > 0 && UnitsConverter.KnotsFromMps(dist / (time / 1000.0)) > _filterMaxSpeed.Value)
                reasons.Add("MaxSpeed");
        }

        if (_filterMinPeriod.HasValue && last != null && !skip)
        {
            long lastFixMs = last.FixTime.HasValue
                ? new DateTimeOffset(last.FixTime.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() : 0;
            long diff = fixMs - lastFixMs;
            if (diff > 0 && diff < _filterMinPeriod.Value * 1000L)
                reasons.Add("MinPeriod");
        }

        if (reasons.Count == 0) return false;

        logger.LogInformation("Position filtered [{Reasons}] device={DeviceId}",
            string.Join(", ", reasons), position.DeviceId);
        return true;
    }

    private bool IsDuplicate(Position position, Position? last)
    {
        if (last == null || position.FixTime != last.FixTime) return false;
        foreach (var key in position.Attributes.Keys)
        {
            if (!last.HasAttribute(key)) return false;
        }
        return true;
    }

    private bool CanSkip(Position position, Position? last)
    {
        if (_skipLimit.HasValue && last != null
                && (position.ServerTime - last.ServerTime).TotalSeconds > _skipLimit.Value)
            return true;

        if (!string.IsNullOrEmpty(_skipAttributes))
        {
            foreach (var attr in _skipAttributes.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!position.HasAttribute(attr)) continue;
                var val = position.Attributes[attr];
                var prev = last != null && last.HasAttribute(attr) ? last.Attributes[attr] : null;
                if (!Equals(val, prev)) return true;
            }
        }
        return false;
    }
}
