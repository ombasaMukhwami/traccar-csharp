namespace Traccar.Protocols.Helpers;

public static class UnitsConverter
{
    private const double KnotsToKphRatio = 0.539957;
    private const double KnotsToMphRatio = 0.868976;
    private const double KnotsToMpsRatio = 1.94384;

    public static double KnotsFromKph(double value) => value * KnotsToKphRatio;

    public static double KnotsFromMph(double value) => value * KnotsToMphRatio;

    public static double KnotsFromMps(double value) => value * KnotsToMpsRatio;
}
