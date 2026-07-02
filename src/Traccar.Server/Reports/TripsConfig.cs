using Microsoft.Extensions.Configuration;
using Traccar.Protocols;

namespace Traccar.Server.Reports;

/// <summary>
/// Trip/stop detection thresholds. Mirrors Java's TripsConfig — values come from the
/// "Report:Trip:*" section of appsettings.json (Java reads the same keys from device/user attributes
/// with global config as fallback; we use global config only for now).
/// </summary>
public sealed class TripsConfig
{
    /// <summary>Minimum trip distance in metres before a movement is confirmed as a trip.</summary>
    public double MinimalTripDistance { get; }

    /// <summary>Minimum trip duration in milliseconds.</summary>
    public long MinimalTripDuration { get; }

    /// <summary>Minimum parking duration in milliseconds before a stop is confirmed.</summary>
    public long MinimalParkingDuration { get; }

    /// <summary>Gap longer than this (ms) between positions is treated as a stop.</summary>
    public long MinimalNoDataDuration { get; }

    /// <summary>If true, the ignition attribute reinforces trip/stop boundaries.</summary>
    public bool UseIgnition { get; }

    /// <summary>If true, use server-calculated totalDistance instead of device odometer.</summary>
    public bool IgnoreOdometer { get; }

    public TripsConfig(IConfiguration configuration)
    {
        MinimalTripDistance = configuration.GetValue(ConfigKeys.Report.Trip.MinimalDistance, 500.0);
        MinimalTripDuration = configuration.GetValue(ConfigKeys.Report.Trip.MinimalDuration, 300L) * 1000L;
        MinimalParkingDuration = configuration.GetValue(ConfigKeys.Report.Trip.MinimalParking, 300L) * 1000L;
        MinimalNoDataDuration = configuration.GetValue(ConfigKeys.Report.Trip.MinimalNoData, 3600L) * 1000L;
        UseIgnition = configuration.GetValue(ConfigKeys.Report.Trip.UseIgnition, false);
        IgnoreOdometer = configuration.GetValue(ConfigKeys.Report.Trip.IgnoreOdometer, false);
    }
}
