using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Gl200;

/// <summary>
/// Covers the GL200 (Queclink) text protocol's FRI-family periodic position reports
/// (FRI/CTN/GEO/RTL/DOG/STR) and the generic alarm/event fallback used by simple report types
/// (SOS, speeding, towing, idle, power on/off, low battery, geofence, temperature, jamming).
/// OBD, CAN bus, WiFi/GSM scan dumps, driver ID, firmware queries, and the separate binary
/// protocol variant are not ported.
/// </summary>
public sealed class Gl200ProtocolDecoder(ConnectionManager connectionManager, ILogger<Gl200ProtocolDecoder> logger)
    : BaseProtocolDecoder("gl200", connectionManager, logger)
{
    private static readonly Regex LocationPattern = new(
        @"(\d{1,2}\.?\d?)?,(\d{1,3}\.\d)?,(\d{1,3}\.?\d?)?,(-?\d{1,5}\.\d)?,(-?\d{1,3}\.\d{6})?,(-?\d{1,2}\.\d{6})?" +
        @"(?:(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2}))?(?:,\d+((?:,[0-9a-fA-F]{12},-\d+,,,)+))?,(\d+)?,(\d+)?," +
        @"(?:(\d+),(\d+),|([0-9a-fA-F]+)?,([0-9a-fA-F]+)?,)(?:\d+|(\d+\.\d))?,",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex FriPattern = Parser.Compile(
        @"\+(?:RESP|BUFF):GT\w{3},(?:.{6}|.{10})?,(\d{15}|[0-9a-fA-F]{14}),(?:([0-9A-Z]{17}),)?[^,]*,(\d+)?," +
        @"(?:(\d{1,2}),)?(?:\d{1,2},)?(?:\d*,)?(?:(\d+),)?((?:" + LocationPattern + @")+).*" +
        @"(\d{4})(\d{2})(\d{2})(?:(\d{2})(\d{2})(\d{2}))?,([0-9a-fA-F]{4})\$?");

    private void DecodeLocation(Position position, Parser parser)
    {
        var hdop = parser.NextDouble();
        position.Valid = hdop is null or > 0;
        position.Set(Position.KeyHdop, hdop);

        position.Speed = UnitsConverter.KnotsFromKph(parser.NextDouble(0));
        position.Course = parser.NextDouble(0);
        position.Altitude = parser.NextDouble(0);

        if (parser.HasNext(8))
        {
            position.Valid = true;
            position.Longitude = parser.NextDouble()!.Value;
            position.Latitude = parser.NextDouble()!.Value;
            position.SetTime(parser.NextDateTime());
        }
        else
        {
            GetLastLocation(position, null);
        }

        var network = new Network();

        if (parser.HasNext())
        {
            var values = JavaString.Split(parser.Next()!, ',');
            for (var i = 0; i + 2 < values.Length; i += 5)
            {
                var mac = InsertColons(values[i + 1]);
                network.AddWifiAccessPoint(WifiAccessPoint.From(mac, int.Parse(values[i + 2])));
            }
        }

        if (parser.HasNext(6))
        {
            var mcc = parser.NextInt();
            var mnc = parser.NextInt();
            if (parser.HasNext(2))
            {
                network.AddCellTower(CellTower.From(mcc ?? 0, mnc ?? 0, parser.NextInt(0), parser.NextInt(0)));
            }
            if (parser.HasNext(2))
            {
                network.AddCellTower(CellTower.From(mcc ?? 0, mnc ?? 0, parser.NextHexInt(0), parser.NextHexInt(0)));
            }
        }

        if (network.WifiAccessPoints != null || network.CellTowers != null)
        {
            position.Network = network;
        }

        if (parser.HasNext())
        {
            position.Set(Position.KeyOdometer, parser.NextDouble()!.Value * 1000);
        }
    }

    private static string InsertColons(string mac)
    {
        var withColons = Regex.Replace(mac, "(..)", "$1:");
        return withColons.TrimEnd(':');
    }

    private object? DecodeFri(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(FriPattern, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        var vin = parser.Next();
        var power = parser.NextInt();
        _ = parser.NextInt(); // report type (motion/charge bits not decoded in this build)
        var battery = parser.NextInt();

        var positions = new List<Position>();
        foreach (Match locationMatch in LocationPattern.Matches(parser.Next()!))
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            if (!string.IsNullOrEmpty(vin))
            {
                position.Set(Position.KeyVin, vin);
            }
            DecodeLocation(position, new Parser(locationMatch));
            positions.Add(position);
        }

        if (positions.Count == 0)
        {
            return null;
        }

        parser.Skip(20); // location group's own 20 nested capture groups, already consumed via LocationPattern above

        var last = positions[^1];

        if (power is > 10)
        {
            last.Set(Position.KeyPower, power.Value / 1000.0);
        }
        if (battery != null)
        {
            last.Set(Position.KeyBatteryLevel, battery);
        }

        if (parser.HasNext(6))
        {
            last.DeviceTime = parser.NextDateTime();
        }

        return positions;
    }

    private object? DecodeBasic(IChannel channel, EndPoint? remoteAddress, string[] v, string type)
    {
        var index = 0;
        index += 1; // header
        index += 1; // protocol version

        var deviceSession = GetDeviceSession(channel, remoteAddress, v[index++]);
        if (deviceSession == null)
        {
            return null;
        }
        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        if (index + 2 < v.Length && Regex.IsMatch(v[index + 2], "^[0-9A-Fa-f]{1,2}$"))
        {
            var reportType = Convert.ToInt32(v[index + 2], 16);
            switch (type)
            {
                case "NMR":
                    position.Set(Position.KeyMotion, reportType == 1);
                    break;
                case "DIS":
                    position.Set(Position.PrefixIn + reportType / 0x10, reportType % 0x10 == 1);
                    break;
                case "IGL":
                    position.Set(Position.KeyIgnition, reportType % 0x10 == 1);
                    break;
                case "HBM":
                    switch (reportType % 0x10)
                    {
                        case 0 or 3: position.AddAlarm(Position.AlarmBraking); break;
                        case 1 or 4: position.AddAlarm(Position.AlarmAcceleration); break;
                        case 2: position.AddAlarm(Position.AlarmCornering); break;
                    }
                    break;
            }
        }

        while (index + 2 < v.Length)
        {
            if (Regex.IsMatch(v[index], @"^-?\d{1,3}\.\d{6}$") && Regex.IsMatch(v[index + 1], @"^-?\d{1,3}\.\d{6}$"))
            {
                index -= 4;
                position.Valid = true;
                position.Set(Position.KeyHdop, int.Parse(v[index++]));
                position.Speed = UnitsConverter.KnotsFromKph(double.Parse(v[index++], CultureInfo.InvariantCulture));
                position.Course = int.Parse(v[index++]);
                position.Altitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Longitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Latitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.SetTime(DateTime.ParseExact(v[index++], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
                break;
            }
            index += 1;
        }
        if (!position.HasAttribute(Position.KeyHdop))
        {
            GetLastLocation(position, null);
            index = 2;
        }

        while (index + 3 < v.Length)
        {
            if (Regex.IsMatch(v[index], @"^\d{4}$") && Regex.IsMatch(v[index + 1], @"^\d{4}$")
                && Regex.IsMatch(v[index + 2], @"^[0-9A-Fa-f]{4}$") && Regex.IsMatch(v[index + 3], @"^[0-9A-Fa-f]{4,8}$"))
            {
                var network = new Network();
                network.AddCellTower(CellTower.From(
                    int.Parse(v[index++]), int.Parse(v[index++]),
                    Convert.ToInt32(v[index++], 16), Convert.ToInt64(v[index++], 16)));
                position.Network = network;
                break;
            }
            index += 1;
        }

        index = v.Length - 2;
        if (index >= 0 && v[index].Length == 14)
        {
            position.DeviceTime = DateTime.ParseExact(
                v[index], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            if (position.HasAttribute(Position.KeyHdop) && index > 0 && Regex.IsMatch(v[index - 1], @"^\d{1,3}$"))
            {
                position.Set(Position.KeyBatteryLevel, int.Parse(v[index - 1]));
            }
        }

        switch (type)
        {
            case "SOS": position.AddAlarm(Position.AlarmSos); break;
            case "SPD": position.AddAlarm(Position.AlarmOverspeed); break;
            case "TOW": position.AddAlarm(Position.AlarmTow); break;
            case "IDL": position.AddAlarm(Position.AlarmIdle); break;
            case "PNA": position.AddAlarm(Position.AlarmPowerOn); break;
            case "PFA": position.AddAlarm(Position.AlarmPowerOff); break;
            case "EPN" or "MPN": position.AddAlarm(Position.AlarmPowerRestored); break;
            case "EPF" or "MPF": position.AddAlarm(Position.AlarmPowerCut); break;
            case "BPL": position.AddAlarm(Position.AlarmLowBattery); break;
            case "STT": position.AddAlarm(Position.AlarmMovement); break;
            case "SWG": position.AddAlarm(Position.AlarmGeofence); break;
            case "TMP" or "TEM": position.AddAlarm(Position.AlarmTemperature); break;
            case "JDR" or "JDS": position.AddAlarm(Position.AlarmJamming); break;
        }

        return position.Attributes.Count > 0 || position.Network != null ? position : null;
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var sentence = Regex.Replace(((IByteBuffer)message).ToString(Encoding.ASCII), @"\$$", "");

        var typeIndex = sentence.IndexOf(":GT", StringComparison.Ordinal);
        if (typeIndex < 0)
        {
            return null;
        }

        var values = JavaString.Split(sentence, ',');
        var type = sentence.Substring(typeIndex + 3, 3);

        object? result = type switch
        {
            "CTN" or "FRI" or "GEO" or "RTL" or "DOG" or "STR" => DecodeFri(channel, remoteAddress, sentence),
            _ => null,
        };

        result ??= DecodeBasic(channel, remoteAddress, values, type);

        switch (result)
        {
            case Position position:
                position.Set(Position.KeyType, type);
                break;
            case IEnumerable<Position> positions:
                foreach (var p in positions)
                {
                    p.Set(Position.KeyType, type);
                }
                break;
        }

        return result;
    }
}
