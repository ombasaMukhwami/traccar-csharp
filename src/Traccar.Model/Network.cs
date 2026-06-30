namespace Traccar.Model;

public class Network
{
    public Network() { }

    public Network(params CellTower[] cellTowers)
    {
        foreach (var tower in cellTowers)
        {
            AddCellTower(tower);
        }
    }

    public Network(params WifiAccessPoint[] wifiAccessPoints)
    {
        foreach (var accessPoint in wifiAccessPoints)
        {
            AddWifiAccessPoint(accessPoint);
        }
    }

    public int? HomeMobileCountryCode { get; set; }

    public int? HomeMobileNetworkCode { get; set; }

    public string RadioType { get; set; } = "gsm";

    public string? Carrier { get; set; }

    public bool ConsiderIp { get; set; }

    public HashSet<CellTower>? CellTowers { get; set; }

    public void AddCellTower(CellTower cellTower)
    {
        CellTowers ??= [];
        CellTowers.Add(cellTower);
    }

    public HashSet<WifiAccessPoint>? WifiAccessPoints { get; set; }

    public void AddWifiAccessPoint(WifiAccessPoint wifiAccessPoint)
    {
        WifiAccessPoints ??= [];
        WifiAccessPoints.Add(wifiAccessPoint);
    }
}
