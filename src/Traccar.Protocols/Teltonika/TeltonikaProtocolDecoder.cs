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

namespace Traccar.Protocols.Teltonika;

/// <summary>
/// Covers Teltonika's standard AVL protocol over TCP and UDP (codecs 8, 8 extended, 12, 13, 16, and
/// the older GH3000 codec). One instance is registered per transport, mirroring Java's
/// TeltonikaProtocolDecoder(Protocol, boolean connectionless).
/// </summary>
public sealed class TeltonikaProtocolDecoder : BaseProtocolDecoder
{
    private readonly bool connectionless;
    private readonly bool extended;

    public TeltonikaProtocolDecoder(
        ConnectionManager connectionManager, ILogger<TeltonikaProtocolDecoder> logger,
        IConfiguration configuration, bool connectionless)
        : base("teltonika", connectionManager, logger)
    {
        this.connectionless = connectionless;
        extended = configuration.GetValue<bool>($"Protocols:{ProtocolName}:Extended");
    }

    public const int CodecGh3000 = 0x07;
    public const int Codec8 = 0x08;
    public const int Codec8Ext = 0x8E;
    public const int Codec12 = 0x0C;
    public const int Codec13 = 0x0D;
    public const int Codec16 = 0x10;

    private const int ImagePacketMax = 2048;

    private static readonly Dictionary<int, List<(Func<string?, bool> Predicate, Action<Position, ByteBuf> Handler)>> Parameters = new();

    private static void Register(int id, Func<string?, bool> predicate, Action<Position, ByteBuf> handler)
    {
        if (!Parameters.TryGetValue(id, out var list))
        {
            list = [];
            Parameters[id] = list;
        }
        list.Add((predicate, handler));
    }

    static TeltonikaProtocolDecoder()
    {
        bool Any(string? m) => true;
        bool FmbXxx(string? m) => m != null && Regex.IsMatch(m, "^FM[B-Z]...$|^MTB100$|^MSP500$");
        bool Fmb6Xx(string? m) => m != null && Regex.IsMatch(m, "^FM.6..$");
        bool TatXxx(string? m) => m != null && Regex.IsMatch(m, "^T.T...$");
        bool FmbXxxNot6(string? m) => FmbXxx(m) && !Fmb6Xx(m);
        bool FmbOrTat(string? m) => FmbXxx(m) || TatXxx(m);

        Register(1, Any, (p, b) => p.Set(Position.PrefixIn + 1, b.ReadUnsignedByte() > 0));
        Register(2, Any, (p, b) => p.Set(Position.PrefixIn + 2, b.ReadUnsignedByte() > 0));
        Register(3, Any, (p, b) => p.Set(Position.PrefixIn + 3, b.ReadUnsignedByte() > 0));
        Register(4, Any, (p, b) => p.Set(Position.PrefixIn + 4, b.ReadUnsignedByte() > 0));
        Register(9, FmbXxx, (p, b) => p.Set(Position.PrefixAdc + 1, b.ReadUnsignedShort() / 1000.0));
        Register(10, FmbXxx, (p, b) => p.Set(Position.PrefixAdc + 2, b.ReadUnsignedShort() / 1000.0));
        Register(11, FmbXxx, (p, b) => p.Set(Position.KeyIccid, b.ReadLong().ToString()));
        Register(12, FmbXxx, (p, b) => p.Set(Position.KeyFuelUsed, b.ReadUnsignedInt() / 1000.0));
        Register(13, FmbXxx, (p, b) => p.Set(Position.KeyFuelConsumption, b.ReadUnsignedShort() / 100.0));
        Register(16, Any, (p, b) => p.Set(Position.KeyOdometer, b.ReadUnsignedInt()));
        Register(17, Any, (p, b) => p.Set("axisX", b.ReadShort()));
        Register(18, Any, (p, b) => p.Set("axisY", b.ReadShort()));
        Register(19, Any, (p, b) => p.Set("axisZ", b.ReadShort()));
        Register(21, Any, (p, b) => p.Set(Position.KeyRssi, b.ReadUnsignedByte()));
        Register(24, FmbXxx, (p, b) => p.Speed = UnitsConverter.KnotsFromKph(b.ReadUnsignedShort()));
        Register(25, Any, (p, b) => p.Set("bleTemp1", b.ReadShort() / 100.0));
        Register(26, Any, (p, b) => p.Set("bleTemp2", b.ReadShort() / 100.0));
        Register(27, Any, (p, b) => p.Set("bleTemp3", b.ReadShort() / 100.0));
        Register(28, Any, (p, b) => p.Set("bleTemp4", b.ReadShort() / 100.0));
        Register(30, FmbXxx, (p, b) => p.Set("faultCount", b.ReadUnsignedByte()));
        Register(31, FmbXxx, (p, b) => p.Set(Position.KeyEngineLoad, b.ReadUnsignedByte()));
        Register(32, FmbXxx, (p, b) => p.Set(Position.KeyCoolantTemp, b.ReadByte()));
        Register(36, FmbXxx, (p, b) => p.Set(Position.KeyRpm, b.ReadUnsignedShort()));
        Register(43, FmbXxx, (p, b) => p.Set("milDistance", b.ReadUnsignedShort()));
        Register(57, FmbXxx, (p, b) => p.Set("hybridBatteryLevel", b.ReadByte()));
        Register(66, Any, (p, b) => p.Set(Position.KeyPower, b.ReadUnsignedShort() / 1000.0));
        Register(67, Any, (p, b) => p.Set(Position.KeyBattery, b.ReadUnsignedShort() / 1000.0));
        Register(68, FmbXxx, (p, b) => p.Set("batteryCurrent", b.ReadUnsignedShort() / 1000.0));
        Register(72, FmbXxx, (p, b) => p.Set(Position.PrefixTemp + 1, b.ReadInt() / 10.0));
        Register(73, FmbXxx, (p, b) => p.Set(Position.PrefixTemp + 2, b.ReadInt() / 10.0));
        Register(74, FmbXxx, (p, b) => p.Set(Position.PrefixTemp + 3, b.ReadInt() / 10.0));
        Register(75, FmbXxx, (p, b) => p.Set(Position.PrefixTemp + 4, b.ReadInt() / 10.0));
        Register(78, Any, (p, b) =>
        {
            var driverUniqueId = b.ReadLongLE();
            if (driverUniqueId != 0)
            {
                p.Set(Position.KeyDriverUniqueId, driverUniqueId.ToString("X16"));
            }
        });
        Register(80, FmbXxxNot6, (p, b) => p.Set("dataMode", b.ReadUnsignedByte()));
        Register(81, FmbXxxNot6, (p, b) => p.Set(Position.KeyObdSpeed, b.ReadUnsignedByte()));
        Register(82, FmbXxxNot6, (p, b) => p.Set(Position.KeyThrottle, b.ReadUnsignedByte()));
        Register(83, FmbXxxNot6, (p, b) => p.Set(Position.KeyFuelUsed, b.ReadUnsignedInt() / 10.0));
        Register(84, FmbXxxNot6, (p, b) => p.Set(Position.KeyFuel, b.ReadUnsignedShort() / 10.0));
        Register(85, FmbXxxNot6, (p, b) => p.Set(Position.KeyRpm, b.ReadUnsignedShort()));
        Register(87, FmbXxxNot6, (p, b) => p.Set(Position.KeyObdOdometer, b.ReadUnsignedInt()));
        Register(89, FmbXxxNot6, (p, b) => p.Set(Position.KeyFuelLevel, b.ReadUnsignedByte()));
        Register(107, FmbXxx, (p, b) => p.Set(Position.KeyFuelUsed, b.ReadUnsignedInt() / 10.0));
        Register(110, FmbXxx, (p, b) => p.Set(Position.KeyFuelConsumption, b.ReadUnsignedShort() / 10.0));
        Register(113, FmbXxx, (p, b) => p.Set(Position.KeyBatteryLevel, b.ReadUnsignedByte()));
        Register(115, FmbXxx, (p, b) => p.Set(Position.KeyEngineTemp, b.ReadShort() / 10.0));
        Register(389, m => m == "FMB003", (p, b) => p.Set(Position.KeyObdOdometer, b.ReadUnsignedInt() * 1000));
        Register(701, Fmb6Xx, (p, b) => p.Set("bleTemp1", b.ReadShort() / 10.0));
        Register(702, Fmb6Xx, (p, b) => p.Set("bleTemp2", b.ReadShort() / 10.0));
        Register(703, Fmb6Xx, (p, b) => p.Set("bleTemp3", b.ReadShort() / 10.0));
        Register(704, Fmb6Xx, (p, b) => p.Set("bleTemp4", b.ReadShort() / 10.0));
        Register(179, Any, (p, b) => p.Set(Position.PrefixOut + 1, b.ReadUnsignedByte() > 0));
        Register(180, Any, (p, b) => p.Set(Position.PrefixOut + 2, b.ReadUnsignedByte() > 0));
        Register(181, Any, (p, b) => p.Set(Position.KeyPdop, b.ReadUnsignedShort() / 10.0));
        Register(182, Any, (p, b) => p.Set(Position.KeyHdop, b.ReadUnsignedShort() / 10.0));
        Register(199, Any, (p, b) => p.Set(Position.KeyOdometerTrip, b.ReadUnsignedInt()));
        Register(200, FmbXxx, (p, b) => p.Set("sleepMode", b.ReadUnsignedByte()));
        Register(205, FmbOrTat, (p, b) => p.Set("cid2g", b.ReadUnsignedShort()));
        Register(206, FmbOrTat, (p, b) => p.Set("lac", b.ReadUnsignedShort()));
        Register(236, Any, (p, b) => p.AddAlarm(b.ReadUnsignedByte() > 0 ? Position.AlarmGeneral : null));
        Register(239, Any, (p, b) => p.Set(Position.KeyIgnition, b.ReadUnsignedByte() > 0));
        Register(240, Any, (p, b) => p.Set(Position.KeyMotion, b.ReadUnsignedByte() > 0));
        Register(241, Any, (p, b) => p.Set(Position.KeyOperator, b.ReadUnsignedInt()));
        Register(246, FmbXxx, (p, b) => p.AddAlarm(b.ReadUnsignedByte() > 0 ? Position.AlarmTow : null));
        Register(247, FmbXxx, (p, b) => p.AddAlarm(b.ReadUnsignedByte() > 0 ? Position.AlarmAccident : null));
        Register(249, FmbXxx, (p, b) => p.AddAlarm(b.ReadUnsignedByte() > 0 ? Position.AlarmJamming : null));
        Register(251, FmbXxx, (p, b) => p.AddAlarm(b.ReadUnsignedByte() > 0 ? Position.AlarmIdle : null));
        Register(252, FmbXxx, (p, b) => p.AddAlarm(b.ReadUnsignedByte() > 0 ? Position.AlarmPowerCut : null));
        Register(253, Any, (p, b) =>
        {
            switch (b.ReadUnsignedByte())
            {
                case 1: p.AddAlarm(Position.AlarmAcceleration); break;
                case 2: p.AddAlarm(Position.AlarmBraking); break;
                case 3: p.AddAlarm(Position.AlarmCornering); break;
            }
        });
        Register(175, FmbXxx, (p, b) =>
            p.AddAlarm(b.ReadUnsignedByte() > 0 ? Position.AlarmGeofenceEnter : Position.AlarmGeofenceExit));
        Register(636, FmbOrTat, (p, b) => p.Set("cid4g", b.ReadUnsignedInt()));
        Register(662, FmbXxx, (p, b) => p.Set(Position.KeyDoor, b.ReadUnsignedByte() > 0));
        Register(10644, FmbXxx, (p, b) => p.Set("tempProbe1", b.ReadShort() / 100.0));
        Register(10645, FmbXxx, (p, b) => p.Set("tempProbe2", b.ReadShort() / 100.0));
        Register(10646, FmbXxx, (p, b) => p.Set("tempProbe3", b.ReadShort() / 100.0));
        Register(10647, FmbXxx, (p, b) => p.Set("tempProbe4", b.ReadShort() / 100.0));
        Register(10648, FmbXxx, (p, b) => p.Set("tempProbe5", b.ReadShort() / 100.0));
        Register(10649, FmbXxx, (p, b) => p.Set("tempProbe6", b.ReadShort() / 100.0));
        Register(10800, FmbXxx, (p, b) => p.Set("eyeTemp1", b.ReadShort() / 100.0));
        Register(10801, FmbXxx, (p, b) => p.Set("eyeTemp2", b.ReadShort() / 100.0));
        Register(10802, FmbXxx, (p, b) => p.Set("eyeTemp3", b.ReadShort() / 100.0));
        Register(10803, FmbXxx, (p, b) => p.Set("eyeTemp4", b.ReadShort() / 100.0));
        Register(10832, FmbXxx, (p, b) => p.Set("eyeRoll1", b.ReadShort()));
        Register(10833, FmbXxx, (p, b) => p.Set("eyeRoll2", b.ReadShort()));
        Register(10834, FmbXxx, (p, b) => p.Set("eyeRoll3", b.ReadShort()));
        Register(10835, FmbXxx, (p, b) => p.Set("eyeRoll4", b.ReadShort()));
    }

    private static long ReadValue(ByteBuf buf, int length) => length switch
    {
        1 => buf.ReadUnsignedByte(),
        2 => buf.ReadUnsignedShort(),
        4 => buf.ReadUnsignedInt(),
        _ => buf.ReadLong(),
    };

    private static void DecodeGh3000Parameter(Position position, int id, ByteBuf buf, int length)
    {
        switch (id)
        {
            case 1: position.Set(Position.KeyBatteryLevel, ReadValue(buf, length)); break;
            case 2: position.Set("usbConnected", ReadValue(buf, length) == 1); break;
            case 5: position.Set("uptime", ReadValue(buf, length)); break;
            case 20: position.Set(Position.KeyHdop, ReadValue(buf, length) / 10.0); break;
            case 21: position.Set(Position.KeyVdop, ReadValue(buf, length) / 10.0); break;
            case 22: position.Set(Position.KeyPdop, ReadValue(buf, length) / 10.0); break;
            case 67: position.Set(Position.KeyBattery, ReadValue(buf, length) / 1000.0); break;
            case 221: position.Set("button", ReadValue(buf, length)); break;
            case 222:
                if (ReadValue(buf, length) == 1)
                {
                    position.AddAlarm(Position.AlarmSos);
                }
                break;
            case 240: position.Set(Position.KeyMotion, ReadValue(buf, length) == 1); break;
            case 244: position.Set(Position.KeyRoaming, ReadValue(buf, length) == 1); break;
            default: position.Set(Position.PrefixIo + id, ReadValue(buf, length)); break;
        }
    }

    private static void DecodeParameter(Position position, int id, ByteBuf buf, int length, int codec, string? model)
    {
        if (codec == CodecGh3000)
        {
            DecodeGh3000Parameter(position, id, buf, length);
            return;
        }
        var index = buf.ReaderIndex;
        if (Parameters.TryGetValue(id, out var handlers))
        {
            foreach (var (predicate, handler) in handlers)
            {
                if (predicate(model))
                {
                    handler(position, buf);
                    buf.ReaderIndex = index + length;
                    return;
                }
            }
        }
        position.Set(Position.PrefixIo + id, ReadValue(buf, length));
    }

    private static void DecodeCell(Position position, Network network, string mncKey, string lacKey, string cidKey, string rssiKey)
    {
        if (position.HasAttribute(mncKey) && position.HasAttribute(lacKey) && position.HasAttribute(cidKey))
        {
            var cellTower = CellTower.From(
                0, position.GetInteger(mncKey), position.GetInteger(lacKey), position.GetLong(cidKey));
            cellTower.SignalStrength = position.GetInteger(rssiKey);
            position.Remove(mncKey);
            position.Remove(lacKey);
            position.Remove(cidKey);
            position.Remove(rssiKey);
            network.AddCellTower(cellTower);
        }
    }

    private static void DecodeNetwork(Position position, string? model)
    {
        if (model == "TAT100")
        {
            var network = new Network();
            DecodeCell(position, network, "io1200", "io287", "io288", "io289");
            DecodeCell(position, network, "io1201", "io290", "io291", "io292");
            DecodeCell(position, network, "io1202", "io293", "io294", "io295");
            DecodeCell(position, network, "io1203", "io296", "io297", "io298");
            if (network.CellTowers != null)
            {
                position.Network = network;
            }
        }
        else
        {
            var hasCid2G = position.HasAttribute("cid2g");
            var hasCid4G = position.HasAttribute("cid4g");
            var cid2G = position.GetInteger("cid2g");
            var cid4G = position.GetLong("cid4g");
            var hasLac = position.HasAttribute("lac");
            var lac = position.GetInteger("lac");
            position.Remove("cid2g");
            position.Remove("cid4g");
            position.Remove("lac");
            if (hasLac && (hasCid2G || hasCid4G))
            {
                var network = new Network();
                CellTower cellTower;
                if (hasCid2G)
                {
                    cellTower = CellTower.From(0, 0, lac, cid2G);
                }
                else
                {
                    cellTower = CellTower.From(0, 0, lac, cid4G);
                    network.RadioType = "lte";
                }
                var operatorId = position.GetLong(Position.KeyOperator);
                if (operatorId >= 1000)
                {
                    cellTower.SetOperator(operatorId);
                }
                network.AddCellTower(cellTower);
                position.Network = network;
            }
        }
    }

    private static int ReadExtByte(ByteBuf buf, int codec, params int[] codecs)
    {
        foreach (var c in codecs)
        {
            if (codec == c)
            {
                return buf.ReadUnsignedShort();
            }
        }
        return buf.ReadUnsignedByte();
    }

    private void DecodeLocation(Position position, ByteBuf buf, int codec, string? model)
    {
        var globalMask = 0x0f;

        if (codec == CodecGh3000)
        {
            var time = buf.ReadUnsignedInt() & 0x3fffffff;
            time += 1167609600; // 2007-01-01 00:00:00

            globalMask = buf.ReadUnsignedByte();
            if (BitUtil.Check(globalMask, 0))
            {
                position.SetTime(DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime);

                var locationMask = buf.ReadUnsignedByte();

                if (BitUtil.Check(locationMask, 0))
                {
                    position.Latitude = buf.ReadFloat();
                    position.Longitude = buf.ReadFloat();
                }
                if (BitUtil.Check(locationMask, 1))
                {
                    position.Altitude = buf.ReadUnsignedShort();
                }
                if (BitUtil.Check(locationMask, 2))
                {
                    position.Course = buf.ReadUnsignedByte() * 360.0 / 256;
                }
                if (BitUtil.Check(locationMask, 3))
                {
                    position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedByte());
                }
                if (BitUtil.Check(locationMask, 4))
                {
                    position.Set(Position.KeySatellites, buf.ReadUnsignedByte());
                }

                if (BitUtil.Check(locationMask, 5))
                {
                    var cellTower = CellTower.From(0, 0, buf.ReadUnsignedShort(), buf.ReadUnsignedShort());

                    if (BitUtil.Check(locationMask, 6))
                    {
                        cellTower.SignalStrength = buf.ReadUnsignedByte();
                    }
                    if (BitUtil.Check(locationMask, 7))
                    {
                        cellTower.SetOperator(buf.ReadUnsignedInt());
                    }

                    position.Network = new Network(cellTower);
                }
                else
                {
                    if (BitUtil.Check(locationMask, 6))
                    {
                        position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                    }
                    if (BitUtil.Check(locationMask, 7))
                    {
                        position.Set(Position.KeyOperator, buf.ReadUnsignedInt());
                    }
                }
            }
            else
            {
                GetLastLocation(position, DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime);
            }
        }
        else
        {
            position.SetTime(DateTimeOffset.FromUnixTimeMilliseconds(buf.ReadLong()).UtcDateTime);

            position.Set("priority", buf.ReadUnsignedByte());

            position.Longitude = buf.ReadInt() / 10000000.0;
            position.Latitude = buf.ReadInt() / 10000000.0;
            position.Altitude = buf.ReadShort();
            position.Course = buf.ReadUnsignedShort();

            var satellites = buf.ReadUnsignedByte();
            position.Set(Position.KeySatellites, satellites);

            position.Valid = satellites != 0;

            position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedShort());

            position.Set(Position.KeyEvent, ReadExtByte(buf, codec, Codec8Ext, Codec16));
            if (codec == Codec16)
            {
                buf.ReadUnsignedByte(); // generation type
            }

            ReadExtByte(buf, codec, Codec8Ext); // total IO data records
        }

        // 1-byte parameters
        if (BitUtil.Check(globalMask, 1))
        {
            var cnt = ReadExtByte(buf, codec, Codec8Ext);
            for (var j = 0; j < cnt; j++)
            {
                DecodeParameter(position, ReadExtByte(buf, codec, Codec8Ext, Codec16), buf, 1, codec, model);
            }
        }

        // 2-byte parameters
        if (BitUtil.Check(globalMask, 2))
        {
            var cnt = ReadExtByte(buf, codec, Codec8Ext);
            for (var j = 0; j < cnt; j++)
            {
                DecodeParameter(position, ReadExtByte(buf, codec, Codec8Ext, Codec16), buf, 2, codec, model);
            }
        }

        // 4-byte parameters
        if (BitUtil.Check(globalMask, 3))
        {
            var cnt = ReadExtByte(buf, codec, Codec8Ext);
            for (var j = 0; j < cnt; j++)
            {
                DecodeParameter(position, ReadExtByte(buf, codec, Codec8Ext, Codec16), buf, 4, codec, model);
            }
        }

        // 8-byte parameters
        if (codec is Codec8 or Codec8Ext or Codec16)
        {
            var cnt = ReadExtByte(buf, codec, Codec8Ext);
            for (var j = 0; j < cnt; j++)
            {
                DecodeParameter(position, ReadExtByte(buf, codec, Codec8Ext, Codec16), buf, 8, codec, model);
            }
        }

        // 16-byte parameters
        if (extended)
        {
            var cnt = ReadExtByte(buf, codec, Codec8Ext);
            for (var j = 0; j < cnt; j++)
            {
                var id = ReadExtByte(buf, codec, Codec8Ext, Codec16);
                position.Set(Position.PrefixIo + id, ByteBufferUtil.HexDump(buf.ReadSlice(16)));
            }
        }

        // variable-length parameters
        if (codec == Codec8Ext)
        {
            var cnt = buf.ReadUnsignedShort();
            for (var j = 0; j < cnt; j++)
            {
                var id = buf.ReadUnsignedShort();
                var length = buf.ReadUnsignedShort();
                if (id is 256 or 325)
                {
                    position.Set(Position.KeyVin, buf.ReadSlice(length).ToString(Encoding.ASCII));
                }
                else if (id == 281)
                {
                    position.Set(Position.KeyDtcs, buf.ReadSlice(length).ToString(Encoding.ASCII).Replace(',', ' '));
                }
                else if (id == 385)
                {
                    var data = new ByteBuf(buf.ReadSlice(length));
                    data.ReadUnsignedByte(); // data part
                    var index = 1;
                    while (data.IsReadable())
                    {
                        var flags = data.ReadUnsignedByte();
                        if (BitUtil.From(flags, 4) > 0)
                        {
                            position.Set("beacon" + index + "Uuid", ByteBufferUtil.HexDump(data.ReadSlice(16)));
                            position.Set("beacon" + index + "Major", data.ReadUnsignedShort());
                            position.Set("beacon" + index + "Minor", data.ReadUnsignedShort());
                        }
                        else
                        {
                            position.Set("beacon" + index + "Namespace", ByteBufferUtil.HexDump(data.ReadSlice(10)));
                            position.Set("beacon" + index + "Instance", ByteBufferUtil.HexDump(data.ReadSlice(6)));
                        }
                        position.Set("beacon" + index + "Rssi", (int)data.ReadByte());
                        if (BitUtil.Check(flags, 1))
                        {
                            position.Set("beacon" + index + "Battery", data.ReadUnsignedShort() / 100.0);
                        }
                        if (BitUtil.Check(flags, 2))
                        {
                            position.Set("beacon" + index + "Temp", data.ReadUnsignedShort());
                        }
                        index += 1;
                    }
                }
                else if (id is 548 or 10828 or 10829 or 10831 or 11317)
                {
                    var data = new ByteBuf(buf.ReadSlice(length));
                    data.ReadUnsignedByte(); // header
                    for (var i = 1; data.IsReadable(); i++)
                    {
                        var beacon = new ByteBuf(data.ReadSlice(data.ReadUnsignedByte()));
                        while (beacon.IsReadable())
                        {
                            var parameterId = beacon.ReadUnsignedByte();
                            var parameterLength = beacon.ReadUnsignedByte();
                            switch (parameterId)
                            {
                                case 0:
                                    position.Set("tag" + i + "Rssi", (int)beacon.ReadByte());
                                    break;
                                case 1:
                                    position.Set("tag" + i + "Id", ByteBufferUtil.HexDump(beacon.ReadSlice(parameterLength)));
                                    break;
                                case 2:
                                    var beaconData = new ByteBuf(beacon.ReadSlice(parameterLength));
                                    var flag = beaconData.ReadUnsignedByte();
                                    if (BitUtil.Check(flag, 6))
                                    {
                                        position.Set("tag" + i + "LowBattery", true);
                                    }
                                    if (BitUtil.Check(flag, 7))
                                    {
                                        position.Set("tag" + i + "Voltage", beaconData.ReadUnsignedByte() * 10 + 2000);
                                    }
                                    break;
                                case 5:
                                    position.Set("tag" + i + "Name", beacon.ReadString(parameterLength, Encoding.UTF8));
                                    break;
                                case 6:
                                    position.Set("tag" + i + "Temp", beacon.ReadShort());
                                    break;
                                case 7:
                                    position.Set("tag" + i + "Humidity", beacon.ReadUnsignedByte());
                                    break;
                                case 8:
                                    position.Set("tag" + i + "Magnet", beacon.ReadUnsignedByte() > 0);
                                    break;
                                case 9:
                                    position.Set("tag" + i + "Motion", beacon.ReadUnsignedByte() > 0);
                                    break;
                                case 10:
                                    position.Set("tag" + i + "MotionCount", beacon.ReadUnsignedShort());
                                    break;
                                case 11:
                                    position.Set("tag" + i + "Pitch", (int)beacon.ReadByte());
                                    break;
                                case 12:
                                    position.Set("tag" + i + "AngleRoll", (int)beacon.ReadShort());
                                    break;
                                case 13:
                                    position.Set("tag" + i + "LowBattery", beacon.ReadUnsignedByte());
                                    break;
                                case 14:
                                    position.Set("tag" + i + "Battery", beacon.ReadUnsignedShort());
                                    break;
                                case 15:
                                    position.Set("tag" + i + "Mac", ByteBufferUtil.HexDump(beacon.ReadSlice(6)));
                                    break;
                                default:
                                    beacon.SkipBytes(parameterLength);
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    position.Set(Position.PrefixIo + id, ByteBufferUtil.HexDump(buf.ReadSlice(length)));
                }
            }
        }

        DecodeNetwork(position, model);

        if (model != null && Regex.IsMatch(model, "^FM.6..$"))
        {
            if (position.Attributes.TryGetValue("io195", out var msbObj)
                && position.Attributes.TryGetValue("io196", out var lsbObj))
            {
                var msb = Convert.ToInt64(msbObj);
                var lsb = Convert.ToInt64(lsbObj);
                var bytes = new byte[16];
                BitConverter.GetBytes(msb).CopyTo(bytes, 0);
                BitConverter.GetBytes(lsb).CopyTo(bytes, 8);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes, 0, 8);
                    Array.Reverse(bytes, 8, 8);
                }
                position.Set(Position.KeyDriverUniqueId, Encoding.ASCII.GetString(bytes).TrimEnd('\0'));
            }
        }
    }

    private static void SendImageRequest(IChannel channel, EndPoint? remoteAddress, long id, int offset, int size)
    {
        var response = Unpooled.Buffer();
        response.WriteInt(0);
        response.WriteShort(0);
        response.WriteShort(19); // length
        response.WriteByte(Codec12);
        response.WriteByte(1); // nod
        response.WriteByte(0x0D); // camera
        response.WriteInt(11); // payload length
        response.WriteByte(2); // command
        response.WriteInt((int)id);
        response.WriteInt(offset);
        response.WriteShort(size);
        response.WriteByte(1); // nod
        response.WriteShort(0);

        var checksumBytes = new byte[19];
        response.GetBytes(8, checksumBytes);
        response.WriteShort(Checksum.Crc16(Checksum.Crc16Ibm, checksumBytes));

        WriteResponse(channel, remoteAddress, response);
    }

    private void DecodeSerial(
        IChannel channel, EndPoint? remoteAddress, DeviceSession deviceSession, Position position, ByteBuf buf)
    {
        GetLastLocation(position, null);

        var type = buf.ReadUnsignedByte();
        if (type == 0x0D)
        {
            buf.ReadInt(); // length
            var subtype = buf.ReadUnsignedByte();
            if (subtype == 0x01)
            {
                var photoId = buf.ReadUnsignedInt();
                var photo = NewMediaBuffer(buf.ReadInt());
                SendImageRequest(channel, remoteAddress, photoId, 0, Math.Min(ImagePacketMax, photo.Capacity));
            }
            else if (subtype == 0x02)
            {
                var photoId = buf.ReadUnsignedInt();
                buf.ReadInt(); // offset
                var photo = GetMediaBuffer()!;
                var chunk = new byte[buf.ReadUnsignedShort()];
                buf.ReadBytes(chunk);
                photo.WriteBytes(chunk);
                if (photo.WritableBytes > 0)
                {
                    SendImageRequest(
                        channel, remoteAddress, photoId, photo.WriterIndex, Math.Min(ImagePacketMax, photo.WritableBytes));
                }
                else
                {
                    position.Set(Position.KeyImage, WriteMediaFile(deviceSession.UniqueId, "jpg"));
                }
            }
        }
        else
        {
            position.Set(Position.KeyType, type);

            var length = buf.ReadInt();
            if (BufferUtil.IsPrintable(buf, length))
            {
                var data = JavaString.Trim(buf.ReadString(length, Encoding.ASCII));
                if (data.StartsWith("UUUUww") && data.EndsWith("SSS"))
                {
                    var values = JavaString.Split(data[6..^4], ';');
                    for (var i = 0; i < 8; i++)
                    {
                        position.Set("axle" + (i + 1), double.Parse(values[i], CultureInfo.InvariantCulture));
                    }
                    position.Set("loadTruck", double.Parse(values[8], CultureInfo.InvariantCulture));
                    position.Set("loadTrailer", double.Parse(values[9], CultureInfo.InvariantCulture));
                    position.Set("totalTruck", double.Parse(values[10], CultureInfo.InvariantCulture));
                    position.Set("totalTrailer", double.Parse(values[11], CultureInfo.InvariantCulture));
                }
                else
                {
                    position.Set(Position.KeyResult, data);
                }
            }
            else
            {
                position.Set(Position.KeyResult, ByteBufferUtil.HexDump(buf.ReadSlice(length)));
            }
        }
    }

    private List<Position>? ParseData(IChannel channel, EndPoint? remoteAddress, ByteBuf buf, int locationPacketId, params string?[] imei)
    {
        var positions = new List<Position>();

        if (!connectionless)
        {
            buf.ReadUnsignedInt(); // data length
        }

        var codec = buf.ReadUnsignedByte();
        var count = buf.ReadUnsignedByte();

        var deviceSession = GetDeviceSession(channel, remoteAddress, imei);
        if (deviceSession == null)
        {
            return null;
        }

        for (var i = 0; i < count; i++)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId, Valid = true };

            switch (codec)
            {
                case Codec13:
                    buf.ReadUnsignedByte(); // type
                    var length = buf.ReadInt() - 4;
                    GetLastLocation(position, DateTimeOffset.FromUnixTimeSeconds(buf.ReadUnsignedInt()).UtcDateTime);
                    if (BufferUtil.IsPrintable(buf, length))
                    {
                        var data = BufferUtil.ReadString(buf, length).Trim();
                        if (data.StartsWith("GTSL"))
                        {
                            position.Set(Position.KeyDriverUniqueId, data.Split('|')[4]);
                        }
                        else
                        {
                            position.Set(Position.KeyResult, data);
                        }
                    }
                    else
                    {
                        position.Set(Position.KeyResult, ByteBufferUtil.HexDump(buf.ReadSlice(length)));
                    }
                    break;
                case Codec12:
                    DecodeSerial(channel, remoteAddress, deviceSession, position, buf);
                    break;
                default:
                    DecodeLocation(position, buf, codec, GetDeviceModel(deviceSession));
                    break;
            }

            if (!position.Outdated || position.Attributes.Count > 0)
            {
                positions.Add(position);
            }
        }

        if (codec != Codec12 && codec != Codec13)
        {
            var response = Unpooled.Buffer();
            if (connectionless)
            {
                response.WriteShort(5);
                response.WriteShort(0);
                response.WriteByte(0x01);
                response.WriteByte(locationPacketId);
                response.WriteByte(count);
            }
            else
            {
                response.WriteInt(count);
            }
            WriteResponse(channel, remoteAddress, response);
        }

        return positions.Count > 0 ? positions : null;
    }

    private void ParseIdentification(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var length = buf.ReadUnsignedShort();
        var imei = buf.ToString(buf.ReaderIndex, length, Encoding.ASCII);
        var deviceSession = GetDeviceSession(channel, remoteAddress, imei);

        var response = Unpooled.Buffer(1);
        response.WriteByte(deviceSession != null ? 1 : 0);
        WriteResponse(channel, remoteAddress, response);
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = new ByteBuf((IByteBuffer)message);
        return connectionless ? DecodeUdp(channel, remoteAddress, buf) : DecodeTcp(channel, remoteAddress, buf);
    }

    private object? DecodeTcp(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        if (buf.ReadableBytes == 1 && buf.ReadUnsignedByte() == 0xff)
        {
            return null;
        }
        if (buf.GetUnsignedShort(0) > 0)
        {
            ParseIdentification(channel, remoteAddress, buf);
            return null;
        }

        buf.SkipBytes(4);
        return ParseData(channel, remoteAddress, buf, 0);
    }

    private object? DecodeUdp(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        buf.ReadUnsignedShort(); // length
        buf.ReadUnsignedShort(); // packet id
        buf.ReadUnsignedByte(); // packet type
        var locationPacketId = buf.ReadUnsignedByte();
        var imeiLength = buf.ReadUnsignedShort();
        var imei = buf.ReadSlice(imeiLength).ToString(Encoding.ASCII);

        return ParseData(channel, remoteAddress, buf, locationPacketId, imei);
    }
}
