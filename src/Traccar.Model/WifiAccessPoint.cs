namespace Traccar.Model;

public class WifiAccessPoint
{
    public static WifiAccessPoint From(string macAddress, int _signalStrength, int? channel = null)
    {
        var wifiAccessPoint = new WifiAccessPoint
        {
            MacAddress = macAddress,
            _signalStrength = _signalStrength,
        };
        if (channel.HasValue)
        {
            wifiAccessPoint.Channel = channel;
        }
        return wifiAccessPoint;
    }

    public string? MacAddress { get; set; }

    private int? _signalStrength;

    public int? SignalStrength
    {
        get => _signalStrength;
        set => _signalStrength = value is > 0 ? -value : value;
    }

    public int? Channel { get; set; }
}
