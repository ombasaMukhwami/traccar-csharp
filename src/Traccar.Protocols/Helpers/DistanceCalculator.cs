using Traccar.Model;

namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.DistanceCalculator — Haversine-based geodesic distance helpers.</summary>
public static class DistanceCalculator
{
    private const double EquatorialEarthRadius = 6378137.0;
    private const double DegToRad = Math.PI / 180;

    public static double Distance(Position p1, Position p2) =>
        Distance(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);

    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        double dlong = (lon2 - lon1) * DegToRad;
        double dlat = (lat2 - lat1) * DegToRad;
        double sinDlat = Math.Sin(dlat / 2);
        double sinDlong = Math.Sin(dlong / 2);
        double a = sinDlat * sinDlat
            + Math.Cos(lat1 * DegToRad) * Math.Cos(lat2 * DegToRad) * sinDlong * sinDlong;
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EquatorialEarthRadius * c;
    }

    public static double DistanceToLine(
        double pointLat, double pointLon, double lat1, double lon1, double lat2, double lon2)
    {
        double d0 = Distance(pointLat, pointLon, lat1, lon1);
        double d1 = Distance(lat1, lon1, lat2, lon2);
        double d2 = Distance(lat2, lon2, pointLat, pointLon);
        if (d0 * d0 > d1 * d1 + d2 * d2) return d2;
        if (d2 * d2 > d1 * d1 + d0 * d0) return d0;
        double halfP = (d0 + d1 + d2) * 0.5;
        double area = Math.Sqrt(halfP * (halfP - d0) * (halfP - d1) * (halfP - d2));
        return 2 * area / d1;
    }

    public static double GetLatitudeDelta(double meters) => meters / 111320;

    public static double GetLongitudeDelta(double meters, double latitude) =>
        meters / (111320 * Math.Cos(Math.PI / 180 * latitude));
}
