namespace Traccar.Model;

public class CellTower
{
    public static CellTower From(int mcc, int mnc, int lac, long cid, int? rssi = null)
    {
        var cellTower = new CellTower
        {
            MobileCountryCode = mcc,
            MobileNetworkCode = mnc,
            LocationAreaCode = lac,
            CellId = cid,
        };
        if (rssi.HasValue)
        {
            cellTower.SignalStrength = rssi;
        }
        return cellTower;
    }

    public string? RadioType { get; set; }

    public long? CellId { get; set; }

    public int? LocationAreaCode { get; set; }

    public int? MobileCountryCode { get; set; }

    public int? MobileNetworkCode { get; set; }

    private int? signalStrength;

    public int? SignalStrength
    {
        get => signalStrength;
        set => signalStrength = value is > 0 ? -value : value;
    }

    public void SetOperator(long operatorValue)
    {
        var operatorString = operatorValue.ToString();
        MobileCountryCode = int.Parse(operatorString[..3]);
        MobileNetworkCode = int.Parse(operatorString[3..]);
    }
}
