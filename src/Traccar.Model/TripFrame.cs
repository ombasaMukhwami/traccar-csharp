namespace Traccar.Model;

/// <summary>
/// Port of the Blazor frontend's TripPoint DTO — one playback step within a single detected
/// trip leg's <see cref="Trip.TripViewModel"/>.
/// </summary>
public record TripFrame(double Latitude, double Longitude, DateTime GpsDateTime, string? LocationText, double Speed, double Heading);

/// <summary>
/// Port of the Blazor frontend's Trip DTO — one detected trip leg (a contiguous period of
/// motion, per <c>ReportUtils.DetectTripsAndStopsAsync</c>/<c>TripReportItem</c>), returned by
/// "telemetry/v1/api/Telemetrics/trips". The frontend groups these itself in trip-search UI —
/// the backend must return already-segmented legs, not a flat position list.
/// </summary>
public class Trip
{
    public List<TripFrame> TripViewModel { get; set; } = [];

    /// <summary>Kilometers.</summary>
    public double DistanceCovered { get; set; }

    /// <summary>Km/h.</summary>
    public double AverageSpeed { get; set; }

    public double TotalSeconds { get; set; }

    /// <summary>Formatted "HH:mm:ss", matching TripSummaryCalculator.TotalDuration's format.</summary>
    public string? TripDuration { get; set; }
}

/// <summary>Port of the Blazor frontend's TripSearchRequest DTO.</summary>
public class TripSearchRequest
{
    /// <summary>Maps to <see cref="Device.UniqueId"/> (sent as a number by the frontend).</summary>
    public long Identifier { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}
