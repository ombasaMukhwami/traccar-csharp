namespace Traccar.Model;

public class WifiAccessPoint
{
    public static WifiAccessPoint From(string macAddress, int signalStrength, int? channel = null)
    {
        var wifiAccessPoint = new WifiAccessPoint
        {
            MacAddress = macAddress,
            SignalStrength = signalStrength,
        };
        if (channel.HasValue)
        {
            wifiAccessPoint.Channel = channel;
        }
        return wifiAccessPoint;
    }

    public string? MacAddress { get; set; }

    private int? signalStrength;

    public int? SignalStrength
    {
        get => signalStrength;
        set => signalStrength = value is > 0 ? -value : value;
    }

    public int? Channel { get; set; }
}
