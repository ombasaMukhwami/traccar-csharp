namespace Traccar.Protocols.Helpers;

/// <summary>
/// WGS84 ↔ GCJ-02 ("Mars coordinates") conversion for positions inside mainland China.
/// Mirrors Java's helper.CoordinateUtil.
/// </summary>
public static class CoordinateUtil
{
    private const double Radius = 6378245.0;
    private const double CorrectionParam = 0.00669342162296594323;

    public readonly record struct Coordinate(double Latitude, double Longitude);

    public static Coordinate Wgs84ToGcj02(double latitude, double longitude)
    {
        var offset = Offset(latitude, longitude);
        return new Coordinate(latitude + offset.Latitude, longitude + offset.Longitude);
    }

    private static Coordinate Offset(double latitude, double longitude)
    {
        double latitudeOffset = TransformLatitude(latitude - 35.0, longitude - 105.0);
        double longitudeOffset = TransformLongitude(latitude - 35.0, longitude - 105.0);

        double magic = Math.Sin(latitude / 180.0 * Math.PI);
        magic = 1 - CorrectionParam * magic * magic;
        double sqrtMagic = Math.Sqrt(magic);

        latitudeOffset = latitudeOffset * 180.0
            / (Radius * (1 - CorrectionParam) / (magic * sqrtMagic) * Math.PI);
        longitudeOffset = longitudeOffset * 180.0
            / (Radius / sqrtMagic * Math.Cos(latitude / 180.0 * Math.PI) * Math.PI);

        return new Coordinate(latitudeOffset, longitudeOffset);
    }

    private static double TransformLongitude(double latitude, double longitude)
    {
        double offset = 300.0 + longitude + 2.0 * latitude
            + 0.1 * longitude * longitude + 0.1 * longitude * latitude
            + 0.1 * Math.Sqrt(Math.Abs(longitude));
        offset += (20.0 * Math.Sin(6.0 * longitude * Math.PI) + 20.0 * Math.Sin(2.0 * longitude * Math.PI)) * 2.0 / 3.0;
        offset += (20.0 * Math.Sin(longitude * Math.PI) + 40.0 * Math.Sin(longitude / 3.0 * Math.PI)) * 2.0 / 3.0;
        offset += (150.0 * Math.Sin(longitude / 12.0 * Math.PI) + 300.0 * Math.Sin(longitude / 30.0 * Math.PI)) * 2.0 / 3.0;
        return offset;
    }

    private static double TransformLatitude(double latitude, double longitude)
    {
        double offset = -100.0 + 2.0 * longitude + 3.0 * latitude
            + 0.2 * latitude * latitude + 0.1 * longitude * latitude
            + 0.2 * Math.Sqrt(Math.Abs(longitude));
        offset += (20.0 * Math.Sin(6.0 * longitude * Math.PI) + 20.0 * Math.Sin(2.0 * longitude * Math.PI)) * 2.0 / 3.0;
        offset += (20.0 * Math.Sin(latitude * Math.PI) + 40.0 * Math.Sin(latitude / 3.0 * Math.PI)) * 2.0 / 3.0;
        offset += (160.0 * Math.Sin(latitude / 12.0 * Math.PI) + 320 * Math.Sin(latitude * Math.PI / 30.0)) * 2.0 / 3.0;
        return offset;
    }
}
