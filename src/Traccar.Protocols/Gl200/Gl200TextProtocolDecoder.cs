using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Gl200;

public sealed class Gl200TextProtocolDecoder : BaseProtocolDecoder
{
    private const string DateFormat = "yyyyMMddHHmmss";
    private const DateTimeStyles DateStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private readonly IConfiguration _configuration;
    private readonly bool _ignoreFixTime;

    public Gl200TextProtocolDecoder(
        ConnectionManager connectionManager, IConfiguration configuration, ILogger<Gl200TextProtocolDecoder> logger)
        : base("gl200", connectionManager, logger)
    {
        _configuration = configuration;
        _ignoreFixTime = configuration.GetValue<bool>(
            $"{ConfigKeys.Protocols.SectionPrefix}:{ProtocolName}:{ConfigKeys.Protocols.IgnoreFixTime}");
    }

    private static readonly Dictionary<string, string> ProtocolModels = new()
    {
        ["02"] = "GL200", ["04"] = "GV200", ["06"] = "GV300", ["08"] = "GMT100", ["09"] = "GV50P",
        ["0F"] = "GV55", ["10"] = "GV55 LITE", ["11"] = "GL500", ["1A"] = "GL300", ["1F"] = "GV500",
        ["21"] = "GL200", ["25"] = "GV300", ["27"] = "GV300W", ["28"] = "GL300VC", ["2C"] = "GL300W",
        ["2D"] = "GV500VC", ["2F"] = "GV55", ["4F"] = "GV56", ["30"] = "GL300", ["31"] = "GV65",
        ["35"] = "GV200", ["36"] = "GV500", ["3F"] = "GMT100", ["40"] = "GL500", ["41"] = "GV75W",
        ["42"] = "GT501", ["44"] = "GL530", ["45"] = "GB100", ["50"] = "GV55W", ["52"] = "GL50",
        ["55"] = "GL50B", ["5E"] = "GV500MAP", ["6E"] = "GV310LAU", ["BD"] = "CV200", ["C2"] = "GV600M",
        ["C3"] = "GL320M", ["DC"] = "GV600MG", ["DE"] = "GL500M", ["DF"] = "CV100LG", ["F1"] = "GV350M",
        ["F8"] = "GV800W", ["FC"] = "GV600W", ["802004"] = "GV58LAU", ["802005"] = "GV355CEU",
        ["80201E"] = "GV30CEU",
    };

    private string GetDeviceModel(DeviceSession deviceSession, string protocolVersion)
    {
        var declaredModel = GetDeviceModel(deviceSession);
        if (declaredModel != null)
        {
            return declaredModel.ToUpperInvariant();
        }
        var versionPrefix = protocolVersion.Length > 6 ? protocolVersion[..6] : protocolVersion[..2];
        return ProtocolModels.GetValueOrDefault(versionPrefix, "");
    }

    private Position? InitPosition(Parser parser, IChannel channel, EndPoint? remoteAddress)
    {
        if (parser.Matches())
        {
            var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
            if (deviceSession != null)
            {
                return new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            }
        }
        return null;
    }

    private void DecodeDeviceTime(Position position, Parser parser)
    {
        if (parser.HasNext(6))
        {
            if (_ignoreFixTime)
            {
                position.SetTime(parser.NextDateTime());
            }
            else
            {
                position.DeviceTime = parser.NextDateTime();
            }
        }
    }

    private static void DecodeAnalog(Position position, int index, string adcString)
    {
        if (adcString.StartsWith('F'))
        {
            position.Set("fuel" + index, int.Parse(adcString[1..], CultureInfo.InvariantCulture));
        }
        else
        {
            position.Set(Position.PrefixAdc + index, int.Parse(adcString, CultureInfo.InvariantCulture) / 1000.0);
        }
    }

    private static long? ParseHours(string? hoursString)
    {
        if (!string.IsNullOrEmpty(hoursString))
        {
            var hours = hoursString.Split(':');
            return (int.Parse(hours[0], CultureInfo.InvariantCulture) * 3600L
                + (hours.Length > 1 ? int.Parse(hours[1], CultureInfo.InvariantCulture) * 60L : 0)
                + (hours.Length > 2 ? int.Parse(hours[2], CultureInfo.InvariantCulture) : 0)) * 1000;
        }
        return null;
    }

    private Position? DecodeAck(IChannel channel, EndPoint? remoteAddress, string[] values)
    {
        var deviceSession = GetDeviceSession(channel, remoteAddress, values[2]);
        if (deviceSession == null)
        {
            return null;
        }
        if (values[0] == "+ACK:GTHBD")
        {
            channel?.WriteAndFlushAsync($"+SACK:GTHBD,{values[1]},{values[^1]}$");
        }
        else
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, DateTime.ParseExact(values[^2], DateFormat, CultureInfo.InvariantCulture, DateStyles));
            position.Valid = false;
            position.Set(Position.KeyResult, values[0]);
            return position;
        }
        return null;
    }

    private object? DecodeInf(IChannel channel, EndPoint? remoteAddress, string[] v)
    {
        var index = 0;
        index += 1; // header
        var protocolVersion = v[index++];
        if (protocolVersion.Length > 10)
        {
            return null; // gt300 protocol
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, v[index++]);
        if (deviceSession == null)
        {
            return null;
        }
        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var model = GetDeviceModel(deviceSession, protocolVersion);
        index += 1; // device name

        if (v[index++].Length > 0)
        {
            var state = Convert.ToInt32(v[index - 1], 16);
            switch (state)
            {
                case 0x16 or 0x1A or 0x12:
                    position.Set(Position.KeyIgnition, false);
                    position.Set(Position.KeyMotion, true);
                    break;
                case 0x11:
                    position.Set(Position.KeyIgnition, false);
                    position.Set(Position.KeyMotion, false);
                    break;
                case 0x21:
                    position.Set(Position.KeyIgnition, true);
                    position.Set(Position.KeyMotion, false);
                    break;
                case 0x22:
                    position.Set(Position.KeyIgnition, true);
                    position.Set(Position.KeyMotion, true);
                    break;
                case 0x41: position.Set(Position.KeyMotion, false); break;
                case 0x42: position.Set(Position.KeyMotion, true); break;
            }
        }

        position.Set(Position.KeyIccid, v[index++]);
        if (v[index++].Length > 0)
        {
            position.Set(Position.KeyRssi, int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        index += 1; // signal quality
        index += 1; // external power supply

        if (v[index + 1].Length >= 12)
        {
            index += 1; // ble sensor mac
            position.Set(Position.KeyDeviceTemp, int.Parse(v[index++], CultureInfo.InvariantCulture));
            position.Set(Position.KeyHumidity, int.Parse(v[index++], CultureInfo.InvariantCulture));
        }

        if (v[index++].Length > 0)
        {
            var value = v[index - 1];
            if (value.Contains('.'))
            {
                position.Set(Position.KeyOdometer, double.Parse(value, CultureInfo.InvariantCulture) * 1000);
            }
            else
            {
                position.Set(Position.KeyPower, int.Parse(value, CultureInfo.InvariantCulture) / 1000.0);
            }
        }
        if (model != "GV500VC")
        {
            if (model is "GV350M" or "GV310LAU")
            {
                index += 1; // expand mask or network type
            }
            else if (v[index++].Length > 0)
            {
                position.Set("power2", int.Parse(v[index - 1], CultureInfo.InvariantCulture) / 1000.0);
            }
        }

        if (v[index++].Length > 0)
        {
            position.Set(Position.KeyBattery, double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (v[index++].Length > 0)
        {
            if (int.Parse(v[index++], CultureInfo.InvariantCulture) == 1)
            {
                position.Set(Position.KeyCharge, true);
            }
        }

        if (model == "GV310LAU")
        {
            index += 1; // led state
            index += 1; // power saving mode
            index += 1; // external antenna
            index += 1; // last fix time
            index += 1; // pin mask
            position.Set(Position.PrefixAdc + 1, int.Parse(v[index++], CultureInfo.InvariantCulture));
            position.Set(Position.PrefixAdc + 2, int.Parse(v[index++], CultureInfo.InvariantCulture));
            position.Set(Position.PrefixAdc + 3, int.Parse(v[index++], CultureInfo.InvariantCulture));
        }

        var time = DateTime.ParseExact(v[^2], DateFormat, CultureInfo.InvariantCulture, DateStyles);
        if (_ignoreFixTime)
        {
            position.SetTime(time);
        }
        else
        {
            position.DeviceTime = time;
        }

        GetLastLocation(position, position.DeviceTime);

        return position;
    }

    private static readonly Regex PatternVer = Parser.Compile(new PatternBuilder()
        .Text("+").Expression("(?:RESP|BUFF):GTVER,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Expression("([^,]*),")
        .Number("(xxxx),")
        .Number("(xxxx),")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd),")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeVer(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternVer, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        position.Set("deviceType", parser.Next());
        position.Set(Position.KeyVersionFw, parser.NextHexInt());
        position.Set(Position.KeyVersionHw, parser.NextHexInt());

        GetLastLocation(position, parser.NextDateTime());

        return position;
    }

    private static void SkipLocation(Parser parser) => parser.Skip(20);

    private static readonly Regex LocationPattern = new PatternBuilder()
        .Number("(d{1,2}.?d?)?,")
        .Number("(d{1,3}.d)?,")
        .Number("(d{1,3}.?d?)?,")
        .Number("(-?d{1,5}.d)?,")
        .Number("(-?d{1,3}.d{6})?,")
        .Number("(-?d{1,2}.d{6})?,")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .GroupBegin()
        .Number(",d+")
        .Number("((?:,x{12},-d+,,,)+)")
        .GroupEnd("?")
        .Text(",")
        .Number("(d+)?,")
        .Number("(d+)?,")
        .GroupBegin()
        .Number("(d+),")
        .Number("(d+),")
        .Or()
        .Number("(x+)?,")
        .Number("(x+)?,")
        .GroupEnd()
        .Number("(?:d+|(d+.d))?,")
        .Compile();

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
                network.AddWifiAccessPoint(WifiAccessPoint.From(mac, int.Parse(values[i + 2], CultureInfo.InvariantCulture)));
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

    private int DecodeLocation(Position position, string model, string[] v, int index)
    {
        var hdop = v[index++].Length == 0 ? 0 : double.Parse(v[index - 1], CultureInfo.InvariantCulture);
        position.Set(Position.KeyHdop, hdop);

        position.Speed = UnitsConverter.KnotsFromKph(
            v[index++].Length == 0 ? 0 : double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        position.Course = v[index++].Length == 0 ? 0 : int.Parse(v[index - 1], CultureInfo.InvariantCulture);
        position.Altitude = v[index++].Length == 0 ? 0 : double.Parse(v[index - 1], CultureInfo.InvariantCulture);

        if (v[index].Length > 0)
        {
            position.Valid = true;
            position.Longitude = v[index++].Length == 0 ? 0 : double.Parse(v[index - 1], CultureInfo.InvariantCulture);
            position.Latitude = v[index++].Length == 0 ? 0 : double.Parse(v[index - 1], CultureInfo.InvariantCulture);
            position.SetTime(DateTime.ParseExact(v[index++], DateFormat, CultureInfo.InvariantCulture, DateStyles));
        }
        else
        {
            index += 3;
            GetLastLocation(position, null);
        }

        var network = new Network();

        if (v[index].Length > 0)
        {
            network.AddCellTower(CellTower.From(
                int.Parse(v[index++], CultureInfo.InvariantCulture),
                int.Parse(v[index++], CultureInfo.InvariantCulture),
                Convert.ToInt32(v[index++], 16),
                Convert.ToInt64(v[index++], 16)));
        }
        else
        {
            index += 4;
        }

        if (network.WifiAccessPoints != null || network.CellTowers != null)
        {
            position.Network = network;
        }

        if (model.StartsWith("GL5", StringComparison.Ordinal))
        {
            index += 1; // csq rssi
            index += 1; // csq ber
        }

        if (model != "GL320M" && v[index++].Length > 0)
        {
            var appendMask = int.Parse(v[index - 1], CultureInfo.InvariantCulture);
            if (BitUtil.Check(appendMask, 0))
            {
                position.Set(Position.KeySatellites, int.Parse(v[index++], CultureInfo.InvariantCulture));
            }
            if (BitUtil.Check(appendMask, 1))
            {
                index += 1; // trigger type
            }
        }

        return index;
    }

    private static readonly Regex PatternObd = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTOBD,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("(?:[0-9A-Z]{17})?,")
        .Expression("[^,]{0,20},")
        .Expression("[01],")
        .Number("x{1,8},")
        .Expression("(?:[0-9A-Z]{17})?,")
        .Number("[01],")
        .Number("(?:d{1,5})?,")
        .Number("(?:x{8})?,")
        .Number("(d{1,5})?,")
        .Number("(d{1,3})?,")
        .Number("(-?d{1,3})?,")
        .Number("(d+.?d*|Inf|NaN)?,")
        .Number("(d{1,5})?,")
        .Number("(?:d{1,5})?,")
        .Expression("([01])?,")
        .Number("(d{1,3})?,")
        .Number("(x*),")
        .Number("(d{1,3})?,")
        .Number("(?:d{1,3})?,")
        .Number("(d{1,3})?,")
        .Expression("(?:[0-9A],)?")
        .Number("(d+),")
        .Expression(LocationPattern.ToString())
        .Number("(d{1,7}.d)?,")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeObd(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternObd, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        position.Set(Position.KeyRpm, parser.NextInt());
        position.Set(Position.KeyObdSpeed, parser.NextInt());
        position.Set(Position.PrefixTemp + 1, parser.NextInt());
        position.Set(Position.KeyFuelConsumption, parser.Next());
        position.Set("dtcsClearedDistance", parser.NextInt());
        if (parser.HasNext())
        {
            position.Set("odbConnect", parser.NextInt() == 1);
        }
        position.Set("dtcsNumber", parser.NextInt());
        position.Set("dtcsCodes", parser.Next());
        position.Set(Position.KeyThrottle, parser.NextInt());
        position.Set(Position.KeyFuel, parser.NextInt());
        if (parser.HasNext())
        {
            position.Set(Position.KeyObdOdometer, parser.NextInt() * 1000);
        }

        DecodeLocation(position, parser);

        if (parser.HasNext())
        {
            position.Set(Position.KeyObdOdometer, (int)(parser.NextDouble()!.Value * 1000));
        }

        DecodeDeviceTime(position, parser);

        return position;
    }

    private object? DecodeCan(IChannel channel, EndPoint? remoteAddress, string[] v)
    {
        var index = 0;
        index += 1; // header
        var protocolVersion = v[index++];
        var deviceSession = GetDeviceSession(channel, remoteAddress, v[index++]);
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var model = GetDeviceModel(deviceSession, protocolVersion);
        index += 1; // device name
        index += 1; // report type
        index += 1; // can bus state
        var reportMask = Convert.ToInt64(v[index++], 16);

        if (BitUtil.Check(reportMask, 0))
        {
            position.Set(Position.KeyVin, v[index++]);
        }
        if (BitUtil.Check(reportMask, 1) && v[index++].Length > 0)
        {
            position.Set(Position.KeyIgnition, int.Parse(v[index - 1], CultureInfo.InvariantCulture) > 0);
        }
        if (BitUtil.Check(reportMask, 2) && v[index++].Length > 0)
        {
            position.Set(Position.KeyObdOdometer, int.Parse(v[index - 1][1..], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 3) && v[index++].Length > 0)
        {
            position.Set(Position.KeyFuelUsed, double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 5) && v[index++].Length > 0)
        {
            position.Set(Position.KeyRpm, int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 4) && v[index++].Length > 0)
        {
            position.Set(Position.KeyObdSpeed, int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 6) && v[index++].Length > 0)
        {
            position.Set(Position.KeyCoolantTemp, int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 7) && v[index++].Length > 0)
        {
            var value = v[index - 1];
            if (value.StartsWith("L/H", StringComparison.Ordinal))
            {
                position.Set(Position.KeyFuelConsumption, double.Parse(value[3..], CultureInfo.InvariantCulture));
            }
        }
        if (BitUtil.Check(reportMask, 8) && v[index++].Length > 0)
        {
            position.Set(Position.KeyFuel, double.Parse(v[index - 1][1..], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 9) && v[index++].Length > 0)
        {
            position.Set("range", long.Parse(v[index - 1], CultureInfo.InvariantCulture) * 100);
        }
        if (BitUtil.Check(reportMask, 10) && v[index++].Length > 0)
        {
            position.Set(Position.KeyThrottle, int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 11) && v[index++].Length > 0)
        {
            position.Set(Position.KeyHours, UnitsConverter.MsFromHours(double.Parse(v[index - 1], CultureInfo.InvariantCulture)));
        }
        if (BitUtil.Check(reportMask, 12) && v[index++].Length > 0)
        {
            position.Set(Position.KeyDrivingTime, double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 13) && v[index++].Length > 0)
        {
            position.Set("idleHours", double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 14) && v[index++].Length > 0)
        {
            position.Set("idleFuelConsumption", double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 15) && v[index++].Length > 0)
        {
            position.Set(Position.KeyAxleWeight, int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 16) && v[index++].Length > 0)
        {
            position.Set("tachographInfo", Convert.ToInt32(v[index - 1], 16));
        }
        if (BitUtil.Check(reportMask, 17) && v[index++].Length > 0)
        {
            position.Set("indicators", Convert.ToInt32(v[index - 1], 16));
        }
        if (BitUtil.Check(reportMask, 18) && v[index++].Length > 0)
        {
            position.Set("lights", Convert.ToInt32(v[index - 1], 16));
        }
        if (BitUtil.Check(reportMask, 19) && v[index++].Length > 0)
        {
            position.Set("doors", Convert.ToInt32(v[index - 1], 16));
        }
        if (BitUtil.Check(reportMask, 20) && v[index++].Length > 0)
        {
            position.Set("vehicleOverspeed", double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMask, 21) && v[index++].Length > 0)
        {
            position.Set("engineOverspeed", double.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (model == "GV350M")
        {
            if (BitUtil.Check(reportMask, 22))
            {
                index += 1; // impulse distance
            }
            if (BitUtil.Check(reportMask, 23))
            {
                index += 1; // gross vehicle weight
            }
            if (BitUtil.Check(reportMask, 24))
            {
                index += 1; // catalyst liquid level
            }
        }
        else if (model == "GV355CEU")
        {
            if (BitUtil.Check(reportMask, 22))
            {
                index += 1; // impulse distance
            }
            if (BitUtil.Check(reportMask, 23))
            {
                index += 1; // engine cold starts
            }
            if (BitUtil.Check(reportMask, 24))
            {
                index += 1; // engine all starts
            }
            if (BitUtil.Check(reportMask, 25))
            {
                index += 1; // engine starts by ignition
            }
            if (BitUtil.Check(reportMask, 26))
            {
                index += 1; // total engine cold running time
            }
            if (BitUtil.Check(reportMask, 27))
            {
                index += 1; // handbrake applies during ride
            }
            if (BitUtil.Check(reportMask, 28))
            {
                index += 1; // electric report mask
            }
        }

        long reportMaskExt = 0;
        if (BitUtil.Check(reportMask, 29) && v[index++].Length > 0)
        {
            reportMaskExt = Convert.ToInt64(v[index - 1], 16);
        }
        if (BitUtil.Check(reportMaskExt, 0) && v[index++].Length > 0)
        {
            position.Set("adBlueLevel", double.Parse(v[index - 1][1..], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMaskExt, 1) && v[index++].Length > 0)
        {
            position.Set("axleWeight1", int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMaskExt, 2) && v[index++].Length > 0)
        {
            position.Set("axleWeight3", int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMaskExt, 3) && v[index++].Length > 0)
        {
            position.Set("axleWeight4", int.Parse(v[index - 1], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(reportMaskExt, 4))
        {
            index += 1; // tachograph overspeed
        }
        if (BitUtil.Check(reportMaskExt, 5))
        {
            index += 1; // tachograph motion
        }
        if (BitUtil.Check(reportMaskExt, 6))
        {
            index += 1; // tachograph direction
        }
        if (BitUtil.Check(reportMaskExt, 7) && v[index++].Length > 0)
        {
            position.Set(Position.PrefixAdc + 1, int.Parse(v[index - 1], CultureInfo.InvariantCulture) / 1000.0);
        }
        if (BitUtil.Check(reportMaskExt, 8))
        {
            index += 1; // pedal breaking factor
        }
        if (BitUtil.Check(reportMaskExt, 9))
        {
            index += 1; // engine breaking factor
        }
        if (BitUtil.Check(reportMaskExt, 10))
        {
            index += 1; // total accelerator kick-downs
        }
        if (BitUtil.Check(reportMaskExt, 11))
        {
            index += 1; // total effective engine speed
        }
        if (BitUtil.Check(reportMaskExt, 12))
        {
            index += 1; // total cruise control time
        }
        if (BitUtil.Check(reportMaskExt, 13))
        {
            index += 1; // total accelerator kick-down time
        }
        if (BitUtil.Check(reportMaskExt, 14))
        {
            index += 1; // total brake application
        }
        if (BitUtil.Check(reportMaskExt, 15) && v[index++].Length > 0)
        {
            position.Set("driver1Card", v[index - 1]);
        }
        if (BitUtil.Check(reportMaskExt, 16) && v[index++].Length > 0)
        {
            position.Set("driver2Card", v[index - 1]);
        }
        if (BitUtil.Check(reportMaskExt, 17) && v[index++].Length > 0)
        {
            position.Set("driver1Name", v[index - 1]);
        }
        if (BitUtil.Check(reportMaskExt, 18) && v[index++].Length > 0)
        {
            position.Set("driver2Name", v[index - 1]);
        }
        if (BitUtil.Check(reportMaskExt, 19) && v[index++].Length > 0)
        {
            position.Set("registration", v[index - 1]);
        }
        if (BitUtil.Check(reportMaskExt, 20))
        {
            index += 1; // expansion information
        }
        if (BitUtil.Check(reportMaskExt, 21))
        {
            index += 1; // rapid brakings
        }
        if (BitUtil.Check(reportMaskExt, 22))
        {
            index += 1; // rapid accelerations
        }
        if (BitUtil.Check(reportMaskExt, 23))
        {
            index += 1; // engine torque
        }
        if (BitUtil.Check(reportMaskExt, 24))
        {
            index += 1; // service distance
        }
        if (BitUtil.Check(reportMaskExt, 25))
        {
            index += 1; // ambient temperature
        }
        if (BitUtil.Check(reportMaskExt, 26))
        {
            index += 1; // tachograph driver1 working time mask
        }
        if (BitUtil.Check(reportMaskExt, 27))
        {
            index += 1; // tachograph driver2 working time mask
        }
        if (BitUtil.Check(reportMaskExt, 28))
        {
            index += 1; // dtc codes
        }
        if (BitUtil.Check(reportMaskExt, 29))
        {
            index += 1; // gaseous fuel level
        }
        if (BitUtil.Check(reportMaskExt, 30))
        {
            index += 1; // tachograph information expand
        }

        long reportMaskCan = 0;
        if (BitUtil.Check(reportMaskExt, 31) && v[index++].Length > 0)
        {
            reportMaskCan = Convert.ToInt64(v[index - 1], 16);
        }
        if (BitUtil.Check(reportMaskCan, 0))
        {
            index += 1; // retarder usage
        }
        if (BitUtil.Check(reportMaskCan, 1))
        {
            index += 1; // power mode
        }
        if (BitUtil.Check(reportMaskCan, 2))
        {
            index += 1; // tachograph timestamp
        }

        if (model != "GV355CEU" && BitUtil.Check(reportMask, 30))
        {
            while (v[index].Length == 0)
            {
                index += 1;
            }
            position.Valid = int.Parse(v[index++], CultureInfo.InvariantCulture) > 0;
            if (v[index].Length > 0)
            {
                position.Speed = UnitsConverter.KnotsFromKph(double.Parse(v[index++], CultureInfo.InvariantCulture));
                position.Course = int.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Altitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Longitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Latitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.SetTime(DateTime.ParseExact(v[index++], DateFormat, CultureInfo.InvariantCulture, DateStyles));
            }
            else
            {
                index += 6; // no location
                GetLastLocation(position, null);
            }
        }
        else
        {
            GetLastLocation(position, null);
        }

        if (BitUtil.Check(reportMask, 31))
        {
            index += 4; // cell
            index += 1; // reserved
        }

        index = v.Length - 2;
        var time = DateTime.ParseExact(v[index], DateFormat, CultureInfo.InvariantCulture, DateStyles);
        if (_ignoreFixTime)
        {
            position.SetTime(time);
        }
        else
        {
            position.DeviceTime = time;
        }

        return position;
    }

    private static void DecodeStatus(Position position, long value)
    {
        var ignition = BitUtil.Between(value, 2 * 8, 3 * 8);
        if (BitUtil.Check(ignition, 4))
        {
            position.Set(Position.KeyIgnition, false);
        }
        else if (BitUtil.Check(ignition, 5))
        {
            position.Set(Position.KeyIgnition, true);
        }
        var input = BitUtil.Between(value, 8, 2 * 8);
        var output = BitUtil.To(value, 8);
        position.Set(Position.KeyInput, input);
        position.Set(Position.PrefixIn + 1, BitUtil.Check(input, 1));
        position.Set(Position.PrefixIn + 2, BitUtil.Check(input, 2));
        position.Set(Position.KeyOutput, output);
        position.Set(Position.PrefixOut + 1, BitUtil.Check(output, 0));
        position.Set(Position.PrefixOut + 2, BitUtil.Check(output, 1));
    }

    private static readonly Regex PatternFri = Parser.Compile(new PatternBuilder()
        .Text("+").Expression("(?:RESP|BUFF):GT...,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("(?:([0-9A-Z]{17}),)?")
        .Expression("[^,]*,")
        .Number("(d+)?,")
        .Number("(d{1,2}),").Optional()
        .Number("d{1,2},").Optional()
        .Number("d*,").Optional()
        .Number("(d+),").Optional()
        .Expression("((?:")
        .Expression(LocationPattern.ToString())
        .Expression(")+)")
        .GroupBegin()
        .Number("d{1,2},")
        .Number("(d{1,5})?,")
        .Number("(d{1,3}),")
        .Number("[01],")
        .Number("(?:[01])?,")
        .Number("(-?d{1,2}.d)?,")
        .Or()
        .Number("(d{1,7}.d)?,")
        .Number("(d{5}:dd:dd)?,")
        .Number("(x+)?,")
        .Number("(x+)?,")
        .Number("d*,").Optional()
        .Number("(d{1,3})?,")
        .Number("(x{6})?,")
        .Number("(d+)?,")
        .Number("(?:d+.?d*|Inf|NaN)?,")
        .Number("(d+)?,")
        .Or()
        .Number("(-?d),")
        .Number("(d{1,3}),")
        .Or()
        .Number("(d{1,7}.d)?,").Optional()
        .Number("(d{1,3})?,")
        .GroupEnd()
        .Any()
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeFri(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternFri, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        var positions = new List<Position>();

        var vin = parser.Next();
        var power = parser.NextInt();
        var reportType = parser.NextInt();
        var battery = parser.NextInt();

        foreach (Match match in LocationPattern.Matches(parser.Next()!))
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            position.Set(Position.KeyVin, vin);

            DecodeLocation(position, new Parser(match));

            positions.Add(position);
        }

        var last = positions[^1];

        SkipLocation(parser);

        if (power is > 10)
        {
            last.Set(Position.KeyPower, power.Value / 1000.0);
        }
        if (battery != null)
        {
            last.Set(Position.KeyBatteryLevel, battery);
        }

        if (parser.HasNext())
        {
            last.Set(Position.KeyBattery, parser.NextInt() / 1000.0);
        }
        last.Set(Position.KeyBatteryLevel, parser.NextInt());
        last.Set(Position.PrefixTemp + 1, parser.NextDouble());

        if (parser.HasNext())
        {
            last.Set(Position.KeyOdometer, parser.NextDouble()!.Value * 1000);
        }
        last.Set(Position.KeyHours, ParseHours(parser.Next()));
        last.Set(Position.PrefixAdc + 1, parser.Next());
        last.Set(Position.PrefixAdc + 2, parser.Next());
        last.Set(Position.KeyBatteryLevel, parser.NextInt());

        if (parser.HasNext())
        {
            DecodeStatus(last, parser.NextHexLong()!.Value);
        }

        last.Set(Position.KeyRpm, parser.NextInt());
        last.Set(Position.KeyFuel, parser.NextInt());

        if (parser.HasNext(2))
        {
            if (reportType != null)
            {
                last.Set(Position.KeyMotion, BitUtil.Check(reportType.Value, 0));
                last.Set(Position.KeyCharge, BitUtil.Check(reportType.Value, 1));
            }
            last.Set(Position.KeyRssi, parser.NextInt());
            last.Set(Position.KeyBatteryLevel, parser.NextInt());
        }
        if (parser.HasNext())
        {
            last.Set(Position.KeyOdometer, parser.NextDouble()!.Value * 1000);
        }
        last.Set(Position.KeyBatteryLevel, parser.NextInt());

        DecodeDeviceTime(last, parser);
        if (_ignoreFixTime)
        {
            positions.Clear();
            positions.Add(last);
        }

        return positions;
    }

    private object? DecodeEri(IChannel channel, EndPoint? remoteAddress, string[] v)
    {
        var index = 0;
        index += 1; // header
        var protocolVersion = v[index++];
        var deviceSession = GetDeviceSession(channel, remoteAddress, v[index++]);
        if (deviceSession == null)
        {
            return null;
        }

        var model = GetDeviceModel(deviceSession, protocolVersion);
        index += 1; // device name
        var mask = Convert.ToInt64(v[index++], 16);
        double? power = v[index++].Length == 0 ? null : int.Parse(v[index - 1], CultureInfo.InvariantCulture) / 1000.0;
        index += 1; // report type

        var count = int.Parse(v[index++], CultureInfo.InvariantCulture);
        var positions = new List<Position>();
        for (var i = 0; i < count; i++)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            index = DecodeLocation(position, model, v, index);
            positions.Add(position);
        }

        var last = positions[^1];
        last.Set(Position.KeyPower, power);

        if (!model.StartsWith("GL5", StringComparison.Ordinal))
        {
            last.Set(Position.KeyOdometer, v[index++].Length == 0 ? null : double.Parse(v[index - 1], CultureInfo.InvariantCulture) * 1000);
        }
        if (!model.StartsWith("GL5", StringComparison.Ordinal) && model != "GL320M")
        {
            last.Set(Position.KeyHours, ParseHours(v[index++]));
            if (v[index++].Length > 0)
            {
                DecodeAnalog(last, 1, v[index - 1]);
            }
        }
        if (model.StartsWith("GV", StringComparison.Ordinal) && !model.StartsWith("GV6", StringComparison.Ordinal) && model != "GV350M")
        {
            if (v[index++].Length > 0)
            {
                DecodeAnalog(last, 2, v[index - 1]);
            }
        }
        if (model is "GV200" or "GV310LAU")
        {
            if (v[index++].Length > 0)
            {
                DecodeAnalog(last, 3, v[index - 1]);
            }
        }
        if ((model.StartsWith("GV3", StringComparison.Ordinal) && model.EndsWith("CEU", StringComparison.Ordinal))
            || model.StartsWith("GV600M", StringComparison.Ordinal))
        {
            index += 1; // reserved
        }

        if (model.StartsWith("GL5", StringComparison.Ordinal))
        {
            last.Set(Position.KeyBatteryLevel, v[index++].Length == 0 ? null : int.Parse(v[index - 1], CultureInfo.InvariantCulture));
            index += 1; // mode selection
            last.Set(Position.KeyMotion, v[index++].Length == 0 ? null : int.Parse(v[index - 1], CultureInfo.InvariantCulture) > 0);
        }
        else if (model == "GV200")
        {
            last.Set(Position.KeyInput, v[index++].Length == 0 ? null : Convert.ToInt32(v[index - 1], 16));
            last.Set(Position.KeyOutput, v[index++].Length == 0 ? null : Convert.ToInt32(v[index - 1], 16));
            index += 1; // uart device type
        }
        else if (model == "GL320M")
        {
            last.Set(Position.KeyBatteryLevel, v[index++].Length == 0 ? null : int.Parse(v[index - 1], CultureInfo.InvariantCulture));
            if (BitUtil.Check(mask, 7))
            {
                last.Set("externalBattery", v[index++].Length == 0 ? null : int.Parse(v[index - 1], CultureInfo.InvariantCulture));
            }
        }
        else
        {
            last.Set(Position.KeyBatteryLevel, v[index++].Length == 0 ? null : int.Parse(v[index - 1], CultureInfo.InvariantCulture));
            if (v[index++].Length > 0)
            {
                DecodeStatus(last, Convert.ToInt64(v[index - 1], 16));
            }
            index += 1; // reserved / uart device type
        }

        var time = DateTime.ParseExact(v[^2], DateFormat, CultureInfo.InvariantCulture, DateStyles);
        if (_ignoreFixTime)
        {
            last.SetTime(time);
            positions.Clear();
            positions.Add(last);
        }
        else
        {
            last.DeviceTime = time;
        }

        if (BitUtil.Check(mask, 0) && model != "GV350M")
        {
            last.Set(Position.KeyFuel, v[index++].Length == 0 ? null : Convert.ToInt32(v[index - 1], 16));
        }

        if (BitUtil.Check(mask, 1))
        {
            var deviceCount = int.Parse(v[index++], CultureInfo.InvariantCulture);
            for (var i = 1; i <= deviceCount; i++)
            {
                index += 1; // id
                index += 1; // type
                if (v[index++].Length > 0)
                {
                    last.Set(Position.PrefixTemp + i, (short)Convert.ToInt32(v[index - 1], 16) * 0.0625);
                }
            }
        }

        if (BitUtil.Check(mask, 2))
        {
            return positions; // can data not supported
        }

        if ((BitUtil.Check(mask, 3) || BitUtil.Check(mask, 4) || (BitUtil.Check(mask, 0) && model == "GV350M"))
            && index < v.Length - 2)
        {
            var deviceCount = int.Parse(v[index++], CultureInfo.InvariantCulture);
            for (var i = 1; i <= deviceCount; i++)
            {
                index += 1; // type
                if (model == "GV350M")
                {
                    index += 1; // uart id
                    if (BitUtil.Check(mask, 0))
                    {
                        last.Set(Position.KeyFuel, Convert.ToInt32(v[index++], 16));
                    }
                }
                if (BitUtil.Check(mask, 3))
                {
                    last.Set(Position.KeyFuel, double.Parse(v[index++], CultureInfo.InvariantCulture));
                }
                if (BitUtil.Check(mask, 4))
                {
                    index += 1; // volume
                }
            }
        }

        if (BitUtil.Check(mask, 7) && model != "GL320M")
        {
            var deviceCount = int.Parse(v[index++], CultureInfo.InvariantCulture);
            for (var i = 1; i <= deviceCount; i++)
            {
                index += 1; // serial number
                var type = int.Parse(v[index++], CultureInfo.InvariantCulture);
                index += 1; // temperature
                if (type == 2)
                {
                    index += 1; // humidity
                }
            }
        }

        if (BitUtil.Check(mask, 8) && model != "GL320M")
        {
            var deviceCount = int.Parse(v[index++], CultureInfo.InvariantCulture);
            for (var i = 1; i <= deviceCount; i++)
            {
                index += 1; // index
                index += 1; // type
                index += 1; // model
                if (model.StartsWith("GV600M", StringComparison.Ordinal))
                {
                    index += 1; // raw data length
                }
                index += 1; // raw data
                var deviceMask = Convert.ToInt32(v[index++], 16);
                if (BitUtil.Check(deviceMask, 0))
                {
                    index += 1; // name
                }
                if (BitUtil.Check(deviceMask, 1))
                {
                    last.Set("tag" + i + "Id", v[index++]);
                }
                if (BitUtil.Check(deviceMask, 2))
                {
                    index += 1; // status
                }
                if (BitUtil.Check(deviceMask, 3))
                {
                    index += 1; // battery level
                }
                if (BitUtil.Check(deviceMask, 4) && v[index++].Length > 0)
                {
                    last.Set("tag" + i + "Temp", double.Parse(v[index - 1], CultureInfo.InvariantCulture));
                }
                if (BitUtil.Check(deviceMask, 5) && v[index++].Length > 0)
                {
                    last.Set("tag" + i + "Humidity", int.Parse(v[index - 1], CultureInfo.InvariantCulture));
                }
                if (BitUtil.Check(deviceMask, 7))
                {
                    index += 1; // input / output
                }
                if (BitUtil.Check(deviceMask, 8))
                {
                    index += 1; // event notification
                }
                if (BitUtil.Check(deviceMask, 9))
                {
                    index += 1; // tire pressure
                }
                if (BitUtil.Check(deviceMask, 10))
                {
                    index += 1; // timestamp
                }
                if (BitUtil.Check(deviceMask, 11))
                {
                    index += 1; // enhanced temperature
                }
                if (BitUtil.Check(deviceMask, 12))
                {
                    index += 1; // magnet
                }
                if (BitUtil.Check(deviceMask, 13) && v[index++].Length > 0)
                {
                    last.Set("tag" + i + "Battery", int.Parse(v[index - 1], CultureInfo.InvariantCulture));
                }
                if (BitUtil.Check(deviceMask, 14))
                {
                    index += 1; // relay
                }
            }
        }

        return positions;
    }

    private object? DecodeIgn(IChannel channel, EndPoint? remoteAddress, string[] v, string type)
    {
        var index = 0;
        index += 1; // header
        var protocolVersion = v[index++];
        var deviceSession = GetDeviceSession(channel, remoteAddress, v[index++]);
        if (deviceSession == null)
        {
            return null;
        }
        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var model = GetDeviceModel(deviceSession, protocolVersion);
        index += 1; // device name
        if (model == "CV200")
        {
            index += 1; // reserved
            index += 1; // report type
        }
        index += 1; // duration of ignition on/off

        index = DecodeLocation(position, model, v, index);

        position.Set(Position.KeyIgnition, type.Contains("GN", StringComparison.Ordinal));
        position.Set(Position.KeyHours, ParseHours(v[index++]));
        if (v[index++].Length > 0)
        {
            position.Set(Position.KeyOdometer, double.Parse(v[index - 1], CultureInfo.InvariantCulture) * 1000);
        }

        var time = DateTime.ParseExact(v[^2], DateFormat, CultureInfo.InvariantCulture, DateStyles);
        if (_ignoreFixTime)
        {
            position.SetTime(time);
        }
        else
        {
            position.DeviceTime = time;
        }

        return position;
    }

    private static readonly Regex PatternLsw = Parser.Compile(new PatternBuilder()
        .Text("+RESP:").Expression("GT[LT]SW,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Number("[01],")
        .Number("([01]),")
        .Expression(LocationPattern.ToString())
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeLsw(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternLsw, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        position.Set(Position.PrefixIn + (sentence.Contains("LSW", StringComparison.Ordinal) ? 1 : 2), parser.NextInt() == 1);

        DecodeLocation(position, parser);

        DecodeDeviceTime(position, parser);

        return position;
    }

    private static readonly Regex PatternIda = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTIDA,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,,")
        .Number("([^,]+),")
        .Expression("[01],")
        .Number("1,")
        .Expression(LocationPattern.ToString())
        .Number("(d+.d),")
        .Text(",,,,")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeIda(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternIda, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        position.Set(Position.KeyDriverUniqueId, parser.Next());

        DecodeLocation(position, parser);

        position.Set(Position.KeyOdometer, parser.NextDouble()!.Value * 1000);

        DecodeDeviceTime(position, parser);

        return position;
    }

    private static readonly Regex PatternWifi = new("([0-9a-fA-F]{12}),(-?\\d+),,,,", RegexOptions.Compiled);

    private static readonly Regex PatternWif = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTWIF,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Number("(d+),")
        .Number("((?:x{12},-?d+,,,,)+),,,,")
        .Number("(d{1,3}),")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeWif(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternWif, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        GetLastLocation(position, null);

        var network = new Network();

        parser.NextInt(); // count
        foreach (Match match in PatternWifi.Matches(parser.Next()!))
        {
            var mac = InsertColons(match.Groups[1].Value);
            network.AddWifiAccessPoint(WifiAccessPoint.From(mac, int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)));
        }

        position.Network = network;

        position.Set(Position.KeyBatteryLevel, parser.NextInt());

        return position;
    }

    private static readonly Regex PatternGsm = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTGSM,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("(?:STR|CTN|NMR|RTL),")
        .Expression("(.*)")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeGsm(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternGsm, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        GetLastLocation(position, null);

        var network = new Network();

        var data = JavaString.Split(parser.Next()!, ',');
        for (var i = 0; i < 6; i++)
        {
            if (data[i * 6].Length > 0)
            {
                network.AddCellTower(CellTower.From(
                    int.Parse(data[i * 6], CultureInfo.InvariantCulture), int.Parse(data[i * 6 + 1], CultureInfo.InvariantCulture),
                    Convert.ToInt32(data[i * 6 + 2], 16), Convert.ToInt32(data[i * 6 + 3], 16),
                    int.Parse(data[i * 6 + 4], CultureInfo.InvariantCulture)));
            }
        }

        position.Network = network;

        return position;
    }

    private static readonly Regex PatternPna = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GT").Expression("P[NF]A,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodePna(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternPna, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        GetLastLocation(position, null);

        position.AddAlarm(sentence.Contains("PNA", StringComparison.Ordinal) ? Position.AlarmPowerOn : Position.AlarmPowerOff);

        return position;
    }

    private static readonly Regex PatternDar = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTDAR,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Number("(d),")
        .Number("(d{1,2}),,,")
        .Expression(LocationPattern.ToString())
        .Any()
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeDar(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternDar, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        var warningType = parser.NextInt();
        var fatigueDegree = parser.NextInt();
        if (warningType == 1)
        {
            position.AddAlarm(Position.AlarmFatigueDriving);
            position.Set("fatigueDegree", fatigueDegree);
        }
        else
        {
            position.Set("warningType", warningType);
        }

        DecodeLocation(position, parser);

        DecodeDeviceTime(position, parser);

        return position;
    }

    private static readonly Regex PatternDtt = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTDTT,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,,,")
        .Number("d,")
        .Number("d+,")
        .Number("(x+),")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeDtt(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternDtt, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        GetLastLocation(position, null);

        var data = Encoding.ASCII.GetString(DataConverter.ParseHex(parser.Next()!));
        if (data.Contains("COMB", StringComparison.Ordinal))
        {
            position.Set(Position.KeyFuel, double.Parse(data.Split(',')[2], CultureInfo.InvariantCulture));
        }
        else
        {
            position.Set(Position.KeyResult, data);
        }

        DecodeDeviceTime(position, parser);

        return position;
    }

    private static readonly Regex PatternBaa = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTBAA,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Number("x+,")
        .Number("d,")
        .Number("d,")
        .Number("x+,")
        .Number("(x{4}),")
        .Expression("((?:[^,]+,){0,6})")
        .Expression(LocationPattern.ToString())
        .Any()
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeBaa(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternBaa, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        var mask = parser.NextHexInt()!.Value;
        var values = parser.Next()!.Split(',');
        var index = 0;
        if (BitUtil.Check(mask, 0))
        {
            position.Set("accessoryName", values[index++]);
        }
        if (BitUtil.Check(mask, 1))
        {
            position.Set("accessoryMac", values[index++]);
        }
        if (BitUtil.Check(mask, 2))
        {
            position.Set("accessoryStatus", int.Parse(values[index++], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(mask, 3))
        {
            position.Set("accessoryVoltage", int.Parse(values[index++], CultureInfo.InvariantCulture) / 1000.0);
        }
        if (BitUtil.Check(mask, 4))
        {
            position.Set("accessoryTemp", int.Parse(values[index++], CultureInfo.InvariantCulture));
        }
        if (BitUtil.Check(mask, 5))
        {
            position.Set("accessoryHumidity", int.Parse(values[index], CultureInfo.InvariantCulture));
        }

        DecodeLocation(position, parser);

        DecodeDeviceTime(position, parser);

        return position;
    }

    private static readonly Regex PatternBid = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTBID,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Number("d,")
        .Number("d,")
        .Number("(x{4}),")
        .Expression("((?:[^,]+,){0,2})")
        .Expression(LocationPattern.ToString())
        .Any()
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeBid(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternBid, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        var mask = parser.NextHexInt()!.Value;
        var values = parser.Next()!.Split(',');
        var index = 0;
        if (BitUtil.Check(mask, 1))
        {
            position.Set("accessoryMac", values[index++]);
        }
        if (BitUtil.Check(mask, 3))
        {
            position.Set("accessoryVoltage", int.Parse(values[index], CultureInfo.InvariantCulture) / 1000.0);
        }

        DecodeLocation(position, parser);

        DecodeDeviceTime(position, parser);

        return position;
    }

    private static readonly Regex PatternLsa = Parser.Compile(new PatternBuilder()
        .Text("+RESP:GTLSA,")
        .Expression("(?:.{6}|.{10})?,")
        .Number("(d{15}|x{14}),")
        .Expression("[^,]*,")
        .Number("d,")
        .Number("d,")
        .Number("d+,")
        .Expression(LocationPattern.ToString())
        .Number("d+,")
        .Number("(d),")
        .Number("(d+),")
        .Number("[01],")
        .Number("[01]?,")
        .Number("(-?d+.d)?,")
        .Number("(dddd)(dd)(dd)")
        .Number("(dd)(dd)(dd)").Optional(2)
        .Text(",")
        .Number("(xxxx)")
        .Text("$").Optional()
        .ToString());

    private object? DecodeLsa(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var parser = new Parser(PatternLsa, sentence);
        var position = InitPosition(parser, channel, remoteAddress);
        if (position == null)
        {
            return null;
        }

        DecodeLocation(position, parser);

        position.Set("lightLevel", parser.NextInt());
        position.Set(Position.KeyBatteryLevel, parser.NextInt());
        position.Set(Position.PrefixTemp + 1, parser.NextDouble());

        DecodeDeviceTime(position, parser);

        return position;
    }

    private object? DecodeLbs(IChannel channel, EndPoint? remoteAddress, string[] v)
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

        index += 1; // device name
        index += 1; // trigger type
        index += 1; // report type
        position.Set(Position.KeyBatteryLevel, int.Parse(v[index++], CultureInfo.InvariantCulture));
        index += 1; // reserved
        index += 1; // motion status
        index += 1; // reserved
        index += 1; // reserved
        index += 1; // component expansion mask

        var network = new Network();
        for (var i = 0; i <= 6; i++)
        {
            if (v[index + 1].Length > 0)
            {
                network.AddCellTower(CellTower.From(
                    int.Parse(v[index++], CultureInfo.InvariantCulture), int.Parse(v[index++], CultureInfo.InvariantCulture),
                    Convert.ToInt32(v[index++], 16), Convert.ToInt32(v[index++], 16),
                    int.Parse(v[index++], CultureInfo.InvariantCulture)));
            }
            else
            {
                index += 5; // empty
            }
            index += 1; // reserved
        }
        position.Network = network;

        var time = DateTime.ParseExact(v[^2], DateFormat, CultureInfo.InvariantCulture, DateStyles);
        GetLastLocation(position, time);

        return position;
    }

    private object? DecodeDat(IChannel channel, EndPoint? remoteAddress, string[] v)
    {
        var index = 0;
        index += 1; // header

        var protocolVersion = v[index++];
        var deviceSession = GetDeviceSession(channel, remoteAddress, v[index++]);
        if (deviceSession == null)
        {
            return null;
        }

        var model = GetDeviceModel(deviceSession, protocolVersion);

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        index += 1; // device name
        index += 1; // report type
        index += 1; // reserved
        index += 1; // reserved

        position.Set("data", v[index++]);

        DecodeLocation(position, model, v, index);

        var time = DateTime.ParseExact(v[^2], DateFormat, CultureInfo.InvariantCulture, DateStyles);
        if (_ignoreFixTime)
        {
            position.SetTime(time);
        }
        else
        {
            position.DeviceTime = time;
        }

        return position;
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
                position.Set(Position.KeyHdop, int.Parse(v[index++], CultureInfo.InvariantCulture));
                position.Speed = UnitsConverter.KnotsFromKph(double.Parse(v[index++], CultureInfo.InvariantCulture));
                position.Course = int.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Altitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Longitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.Latitude = double.Parse(v[index++], CultureInfo.InvariantCulture);
                position.SetTime(DateTime.ParseExact(v[index++], DateFormat, CultureInfo.InvariantCulture, DateStyles));
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
                && Regex.IsMatch(v[index + 2], "^[0-9A-Fa-f]{4}$") && Regex.IsMatch(v[index + 3], "^[0-9A-Fa-f]{4,8}$"))
            {
                var network = new Network();
                network.AddCellTower(CellTower.From(
                    int.Parse(v[index++], CultureInfo.InvariantCulture),
                    int.Parse(v[index++], CultureInfo.InvariantCulture),
                    Convert.ToInt32(v[index++], 16),
                    Convert.ToInt64(v[index++], 16)));
                position.Network = network;
                break;
            }
            index += 1;
        }

        index = v.Length - 2;
        if (index >= 0 && v[index].Length == 14)
        {
            var time = DateTime.ParseExact(v[index], DateFormat, CultureInfo.InvariantCulture, DateStyles);
            if (_ignoreFixTime)
            {
                position.SetTime(time);
            }
            else
            {
                position.DeviceTime = time;
            }

            if (position.HasAttribute(Position.KeyHdop) && index > 0 && Regex.IsMatch(v[index - 1], @"^\d{1,3}$"))
            {
                position.Set(Position.KeyBatteryLevel, int.Parse(v[index - 1], CultureInfo.InvariantCulture));
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

        object? result;
        var type = sentence.Substring(typeIndex + 3, 3);
        if (sentence.StartsWith("+ACK", StringComparison.Ordinal))
        {
            result = DecodeAck(channel, remoteAddress, values);
        }
        else
        {
            result = type switch
            {
                "INF" => DecodeInf(channel, remoteAddress, values),
                "OBD" => DecodeObd(channel, remoteAddress, sentence),
                "CAN" => DecodeCan(channel, remoteAddress, values),
                "CTN" or "FRI" or "GEO" or "RTL" or "DOG" or "STR" => DecodeFri(channel, remoteAddress, sentence),
                "ERI" => DecodeEri(channel, remoteAddress, values),
                "IGN" or "IGF" or "VGN" or "VGF" => DecodeIgn(channel, remoteAddress, values, type),
                "LSW" or "TSW" => DecodeLsw(channel, remoteAddress, sentence),
                "IDA" => DecodeIda(channel, remoteAddress, sentence),
                "WIF" => DecodeWif(channel, remoteAddress, sentence),
                "GSM" => DecodeGsm(channel, remoteAddress, sentence),
                "VER" => DecodeVer(channel, remoteAddress, sentence),
                "PNA" or "PFA" => DecodePna(channel, remoteAddress, sentence),
                "DAR" => DecodeDar(channel, remoteAddress, sentence),
                "DTT" => DecodeDtt(channel, remoteAddress, sentence),
                "BAA" => DecodeBaa(channel, remoteAddress, sentence),
                "BID" => DecodeBid(channel, remoteAddress, sentence),
                "LSA" => DecodeLsa(channel, remoteAddress, sentence),
                "LBS" => DecodeLbs(channel, remoteAddress, values),
                "DAT" => DecodeDat(channel, remoteAddress, values),
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
        }

        if (channel != null && _configuration.GetValue<bool>(
                $"{ConfigKeys.Protocols.SectionPrefix}:{ProtocolName}:{ConfigKeys.Protocols.Ack}"))
        {
            channel.WriteAndFlushAsync($"+SACK:{values[^1]}$");
        }

        return result;
    }
}
