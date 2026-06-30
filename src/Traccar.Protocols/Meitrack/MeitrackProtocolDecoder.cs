using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Meitrack;

public sealed class MeitrackProtocolDecoder(ConnectionManager connectionManager, ILogger<MeitrackProtocolDecoder> logger)
    : BaseProtocolDecoder("meitrack", connectionManager, logger)
{
    private static readonly Regex Pattern = Parser.Compile(
        @"\$\$." +                              // flag
        @"\d+," +                                // length
        @"(\d+)," +                              // imei
        @"[0-9a-fA-F]{3}," +                     // command
        @"(?:\d+,)?" +
        @"(\d+)," +                              // event
        @"(-?\d+\.\d+)," +                       // latitude
        @"(-?\d+\.\d+)," +                       // longitude
        @"(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})," + // date (yymmdd) + time (hhmmss)
        @"([AV])," +                             // validity
        @"(\d+)," +                              // satellites
        @"(\d+)," +                              // rssi
        @"(\d+\.?\d*)," +                        // speed
        @"(\d+)," +                              // course
        @"(\d+\.?\d*)," +                        // hdop
        @"(-?\d+)," +                            // altitude
        @"(\d+)," +                              // odometer
        @"(\d+)," +                              // runtime
        @"(\d+)\|" +                             // mcc
        @"(\d+)\|" +                             // mnc
        @"([0-9a-fA-F]+)?\|" +                   // lac
        @"([0-9a-fA-F]+)?," +                    // cid
        @"([0-9a-fA-F]{2})" +                    // input
        @"([0-9a-fA-F]{2})," +                   // output
        @"(?:" +
        @"(\d+\.\d+)\|" +                        // battery
        @"(\d+\.\d+)\|" +                        // power
        @"\d+\.\d+\|" +                          // rtc voltage
        @"\d+\.\d+\|" +                          // mcu voltage
        @"\d+\.\d+," +                           // gps voltage
        @"|" +
        @"([0-9a-fA-F]+)?\|" +                   // adc1
        @"([0-9a-fA-F]+)?\|" +                   // adc2
        @"([0-9a-fA-F]+)?\|" +                   // adc3
        @"([0-9a-fA-F]+)\|" +                    // battery
        @"([0-9a-fA-F]+)?," +                    // power
        @")" +
        @"(?:" +
        @"(?:([^,]+)?,)?" +                      // event specific
        @"[^,]*," +                              // reserved
        @"(\d+)?," +                             // protocol
        @"([0-9a-fA-F]{4})?" +                   // fuel
        @"(?:" +
        @",([0-9a-fA-F]{6}(?:|[0-9a-fA-F]{6})*)?" + // temperature
        @"(?:" +
        @",(\d+)" +                              // data count
        @",([^*]*)" +                            // data
        @")?" +
        @")?" +
        @"|" +
        @".*" +
        @")" +
        @"\*" +
        @"[0-9a-fA-F]{2}" +
        @"(?:\r\n)?");

    private static string? DecodeAlarm(int @event) => @event switch
    {
        1 => Position.AlarmSos,
        17 => Position.AlarmLowBattery,
        18 => Position.AlarmLowPower,
        19 => Position.AlarmOverspeed,
        20 => Position.AlarmGeofenceEnter,
        21 => Position.AlarmGeofenceExit,
        22 => Position.AlarmPowerRestored,
        23 => Position.AlarmPowerCut,
        36 => Position.AlarmTow,
        44 => Position.AlarmJamming,
        78 => Position.AlarmAccident,
        90 or 91 => Position.AlarmCornering,
        129 => Position.AlarmBraking,
        130 => Position.AlarmAcceleration,
        135 => Position.AlarmFatigueDriving,
        _ => null,
    };

    private Position? DecodeRegular(IChannel channel, EndPoint? remoteAddress, IByteBuffer buf)
    {
        var parser = new Parser(Pattern, buf.ToString(Encoding.ASCII));
        if (!parser.Matches())
        {
            return null;
        }

        var position = new Position(ProtocolName);

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }
        position.DeviceId = deviceSession.DeviceId;

        var @event = parser.NextInt()!.Value;
        position.Set(Position.KeyEvent, @event);
        position.AddAlarm(DecodeAlarm(@event));

        position.Latitude = parser.NextDouble()!.Value;
        position.Longitude = parser.NextDouble()!.Value;

        position.SetTime(parser.NextDateTime());

        position.Valid = parser.Next() == "A";

        position.Set(Position.KeySatellites, parser.NextInt());
        var rssi = parser.NextInt()!.Value;

        position.Speed = UnitsConverter.KnotsFromKph(parser.NextDouble()!.Value);
        position.Course = parser.NextDouble()!.Value;

        position.Set(Position.KeyHdop, parser.NextDouble());

        position.Altitude = parser.NextDouble()!.Value;

        position.Set(Position.KeyOdometer, parser.NextInt());
        position.Set("runtime", parser.Next());

        var mcc = parser.NextInt()!.Value;
        var mnc = parser.NextInt()!.Value;
        var lac = parser.NextHexInt(0);
        var cid = parser.NextHexInt(0);
        if (mcc != 0 && mnc != 0)
        {
            position.Network = new Network(CellTower.From(mcc, mnc, lac, cid, rssi));
        }

        position.Set(Position.KeyInput, parser.NextHexInt());
        position.Set(Position.KeyOutput, parser.NextHexInt());

        if (parser.HasNext(2))
        {
            position.Set(Position.KeyBattery, parser.NextDouble());
            position.Set(Position.KeyPower, parser.NextDouble());
        }
        else
        {
            for (var i = 1; i <= 3; i++)
            {
                position.Set(Position.PrefixAdc + i, parser.NextHexInt());
            }

            switch ((GetDeviceModel(deviceSession) ?? string.Empty).ToUpperInvariant())
            {
                case "MVT340":
                case "MVT380":
                    position.Set(Position.KeyBattery, parser.NextHexInt()!.Value * 3.0 * 2.0 / 1024.0);
                    position.Set(Position.KeyPower, parser.NextHexInt(0) * 3.0 * 16.0 / 1024.0);
                    break;
                case "MT90":
                    position.Set(Position.KeyBattery, parser.NextHexInt()!.Value * 3.3 * 2.0 / 4096.0);
                    position.Set(Position.KeyPower, parser.NextHexInt(0));
                    break;
                case "MT90G":
                    position.Set(Position.KeyBattery, parser.NextHexInt()!.Value * 3.0 * 2.0 / 4096.0);
                    position.Set(Position.KeyPower, parser.NextHexInt(0));
                    break;
                case "T1":
                case "T3":
                case "MVT100":
                case "MVT600":
                case "MVT800":
                case "TC68":
                case "TC68S":
                    position.Set(Position.KeyBattery, parser.NextHexInt()!.Value * 3.3 * 2.0 / 4096.0);
                    position.Set(Position.KeyPower, parser.NextHexInt(0) * 3.3 * 16.0 / 4096.0);
                    break;
                default:
                    position.Set(Position.KeyBattery, parser.NextHexInt()!.Value / 100.0);
                    position.Set(Position.KeyPower, parser.NextHexInt(0) / 100.0);
                    break;
            }
        }

        var eventData = parser.Next();
        if (!string.IsNullOrEmpty(eventData))
        {
            switch (@event)
            {
                case 37:
                    position.Set(Position.KeyDriverUniqueId, eventData);
                    break;
                default:
                    position.Set("eventData", eventData);
                    break;
            }
        }

        var protocol = parser.NextInt(0);

        if (parser.HasNext())
        {
            var fuel = parser.Next()!;
            position.Set(Position.KeyFuel, Convert.ToInt32(fuel[..2], 16) + Convert.ToInt32(fuel[2..], 16) / 100.0);
        }

        if (parser.HasNext())
        {
            foreach (var temp in JavaString.Split(parser.Next()!, '|'))
            {
                var index = Convert.ToInt32(temp[..2], 16);
                if (protocol >= 3)
                {
                    double value = unchecked((short)Convert.ToInt32(temp[2..], 16));
                    position.Set(Position.PrefixTemp + index, value / 100.0);
                }
                else
                {
                    double value = unchecked((sbyte)Convert.ToInt32(temp.Substring(2, 2), 16));
                    value += (value < 0 ? -0.01 : 0.01) * Convert.ToInt32(temp[4..], 16);
                    position.Set(Position.PrefixTemp + index, value);
                }
            }
        }

        if (parser.HasNext(2))
        {
            parser.NextInt(); // count
            DecodeDataFields(position, JavaString.Split(parser.Next()!, ','));
        }

        return position;
    }

    private static void DecodeDataFields(Position position, string[] values)
    {
        if (values.Length > 1 && !string.IsNullOrEmpty(values[1]))
        {
            position.Set("tempData", values[1]);
        }

        if (values.Length > 5 && !string.IsNullOrEmpty(values[5]))
        {
            var data = JavaString.Split(values[5], '|');
            var started = data[0][1] == '0';
            position.Set("taximeterOn", started);
            position.Set("taximeterStart", data[1]);
            if (data.Length > 2)
            {
                position.Set("taximeterEnd", data[2]);
                position.Set("taximeterDistance", int.Parse(data[3]));
                position.Set("taximeterFare", int.Parse(data[4]));
                position.Set("taximeterTrip", data[5]);
                position.Set("taximeterWait", data[6]);
            }
        }
    }

    private List<Position>? DecodeBinaryC(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var positions = new List<Position>();

        var flag = buf.ToString(2, 1, Encoding.ASCII);
        var index = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, (byte)',');

        var imei = buf.ToString(index + 1, 15, Encoding.ASCII);
        var deviceSession = GetDeviceSession(channel, remoteAddress, imei);
        if (deviceSession == null)
        {
            return null;
        }

        buf.SkipBytes(index + 1 + 15 + 1 + 3 + 1 + 2 + 2 + 4);

        while (buf.ReadableBytes >= 0x34)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            position.Set(Position.KeyEvent, buf.ReadUnsignedByte());

            position.Latitude = buf.ReadIntLE() / 1000000.0;
            position.Longitude = buf.ReadIntLE() / 1000000.0;

            position.SetTime(DateTimeOffset.FromUnixTimeSeconds(946684800 + buf.ReadUnsignedIntLE()).UtcDateTime); // 2000-01-01

            position.Valid = buf.ReadUnsignedByte() == 1;

            position.Set(Position.KeySatellites, buf.ReadUnsignedByte());
            var rssi = buf.ReadUnsignedByte();

            position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedShortLE());
            position.Course = buf.ReadUnsignedShortLE();

            position.Set(Position.KeyHdop, buf.ReadUnsignedShortLE() / 10.0);

            position.Altitude = buf.ReadUnsignedShortLE();

            position.Set(Position.KeyOdometer, buf.ReadUnsignedIntLE());
            position.Set("runtime", buf.ReadUnsignedIntLE());

            position.Network = new Network(CellTower.From(
                buf.ReadUnsignedShortLE(), buf.ReadUnsignedShortLE(),
                buf.ReadUnsignedShortLE(), buf.ReadUnsignedShortLE(),
                rssi));

            position.Set(Position.KeyStatus, buf.ReadUnsignedShortLE());

            position.Set(Position.PrefixAdc + 1, buf.ReadUnsignedShortLE());
            position.Set(Position.KeyBattery, buf.ReadUnsignedShortLE() / 100.0);
            position.Set(Position.KeyPower, buf.ReadUnsignedShortLE());

            buf.ReadUnsignedIntLE(); // geo-fence

            positions.Add(position);
        }

        var command = new StringBuilder("@@");
        command.Append(flag).Append(27 + positions.Count / 10).Append(',');
        command.Append(imei).Append(",CCC,").Append(positions.Count).Append('*');
        command.Append(Checksum.Sum(command.ToString()));
        command.Append("\r\n");
        WriteResponse(channel, remoteAddress, Unpooled.CopiedBuffer(command.ToString(), Encoding.ASCII));

        return positions;
    }

    private static string InsertColons(string mac) => Regex.Replace(mac, "(..)", "$1:").TrimEnd(':');

    private List<Position>? DecodeBinaryE(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var positions = new List<Position>();

        buf.ReaderIndex = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, (byte)',') + 1;
        var imei = buf.ReadString(15, Encoding.ASCII);
        buf.SkipBytes(1 + 3 + 1);

        var deviceSession = GetDeviceSession(channel, remoteAddress, imei);
        if (deviceSession == null)
        {
            return null;
        }

        buf.ReadUnsignedIntLE(); // remaining cache
        var count = buf.ReadUnsignedShortLE();

        for (var i = 0; i < count; i++)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            var network = new Network();

            var dataLength = buf.ReadUnsignedShortLE();
            var dataEnd = buf.ReaderIndex + dataLength;
            buf.ReadUnsignedShortLE(); // index

            var paramCount = buf.ReadUnsignedByte();
            for (var j = 0; j < paramCount; j++)
            {
                var extension = buf.GetUnsignedByte(buf.ReaderIndex) == 0xFE;
                var id = extension ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte();
                switch (id)
                {
                    case 0x01: position.Set(Position.KeyEvent, buf.ReadUnsignedByte()); break;
                    case 0x05: position.Valid = buf.ReadUnsignedByte() > 0; break;
                    case 0x06: position.Set(Position.KeySatellites, buf.ReadUnsignedByte()); break;
                    case 0x07: position.Set(Position.KeyRssi, buf.ReadUnsignedByte()); break;
                    case 0x14: position.Set(Position.KeyOutput, buf.ReadUnsignedByte()); break;
                    case 0x15: position.Set(Position.KeyInput, buf.ReadUnsignedByte()); break;
                    case 0x47:
                        var lockState = buf.ReadUnsignedByte();
                        if (lockState > 0)
                        {
                            position.Set(Position.KeyLock, lockState == 2);
                        }
                        break;
                    case 0x97: position.Set(Position.KeyThrottle, buf.ReadUnsignedByte()); break;
                    case 0x9D: position.Set(Position.KeyFuel, buf.ReadUnsignedByte()); break;
                    case 0xFE69: position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte()); break;
                    default: buf.ReadUnsignedByte(); break;
                }
            }

            paramCount = buf.ReadUnsignedByte();
            for (var j = 0; j < paramCount; j++)
            {
                var extension = buf.GetUnsignedByte(buf.ReaderIndex) == 0xFE;
                var id = extension ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte();
                switch (id)
                {
                    case 0x08: position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedShortLE()); break;
                    case 0x09: position.Course = buf.ReadUnsignedShortLE(); break;
                    case 0x0A: position.Set(Position.KeyHdop, buf.ReadUnsignedShortLE()); break;
                    case 0x0B: position.Altitude = buf.ReadShortLE(); break;
                    case 0x16: position.Set(Position.PrefixAdc + 1, buf.ReadUnsignedShortLE() / 100.0); break;
                    case 0x17: position.Set(Position.PrefixAdc + 2, buf.ReadUnsignedShortLE() / 100.0); break;
                    case 0x19: position.Set(Position.KeyBattery, buf.ReadUnsignedShortLE() / 100.0); break;
                    case 0x1A: position.Set(Position.KeyPower, buf.ReadUnsignedShortLE() / 100.0); break;
                    case 0x29: position.Set(Position.KeyFuel, buf.ReadUnsignedShortLE() / 100.0); break;
                    case 0x40: position.Set(Position.KeyEvent, buf.ReadUnsignedShortLE()); break;
                    case 0x91:
                    case 0x92: position.Set(Position.KeyObdSpeed, buf.ReadUnsignedShortLE()); break;
                    case 0x98: position.Set(Position.KeyFuelUsed, buf.ReadUnsignedShortLE()); break;
                    case 0x99: position.Set(Position.KeyRpm, buf.ReadUnsignedShortLE()); break;
                    case 0x9C: position.Set(Position.KeyCoolantTemp, buf.ReadUnsignedShortLE()); break;
                    case 0x9F: position.Set(Position.PrefixTemp + 1, buf.ReadUnsignedShortLE()); break;
                    case 0xC9: position.Set(Position.KeyFuelConsumption, buf.ReadUnsignedShortLE()); break;
                    default: buf.ReadUnsignedShortLE(); break;
                }
            }

            paramCount = buf.ReadUnsignedByte();
            for (var j = 0; j < paramCount; j++)
            {
                var extension = buf.GetUnsignedByte(buf.ReaderIndex) == 0xFE;
                var id = extension ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte();
                switch (id)
                {
                    case 0x02: position.Latitude = buf.ReadIntLE() / 1000000.0; break;
                    case 0x03: position.Longitude = buf.ReadIntLE() / 1000000.0; break;
                    case 0x04:
                        position.SetTime(DateTimeOffset.FromUnixTimeSeconds(946684800 + buf.ReadUnsignedIntLE()).UtcDateTime); // 2000-01-01
                        break;
                    case 0x0C: position.Set(Position.KeyOdometer, buf.ReadUnsignedIntLE()); break;
                    case 0x0D: position.Set("runtime", buf.ReadUnsignedIntLE()); break;
                    case 0x25: position.Set(Position.KeyDriverUniqueId, buf.ReadUnsignedIntLE().ToString()); break;
                    case 0x9B: position.Set(Position.KeyObdOdometer, buf.ReadUnsignedIntLE()); break;
                    case 0xA0: position.Set(Position.KeyFuelUsed, buf.ReadUnsignedIntLE() / 1000.0); break;
                    case 0xA2: position.Set(Position.KeyFuelConsumption, buf.ReadUnsignedIntLE() / 100.0); break;
                    case 0xFEF4: position.Set(Position.KeyHours, buf.ReadUnsignedIntLE() * 60000); break;
                    default: buf.ReadUnsignedIntLE(); break;
                }
            }

            paramCount = buf.ReadUnsignedByte();
            for (var j = 0; j < paramCount; j++)
            {
                var extension = buf.GetUnsignedByte(buf.ReaderIndex) == 0xFE;
                var id = extension ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte();
                var length = buf.ReadUnsignedByte();
                switch (id)
                {
                    case 0x1D:
                    case 0x1E:
                    case 0x1F:
                    case 0x20:
                    case 0x21:
                    case 0x22:
                    case 0x23:
                    case 0x24:
                    case 0x25:
                        var wifiMac = InsertColons(ByteBufferUtil.HexDump(buf.ReadSlice(6)));
                        network.AddWifiAccessPoint(WifiAccessPoint.From(wifiMac, buf.ReadShortLE()));
                        break;
                    case 0x0E:
                    case 0x0F:
                    case 0x10:
                    case 0x12:
                    case 0x13:
                        network.AddCellTower(CellTower.From(
                            buf.ReadUnsignedShortLE(), buf.ReadUnsignedShortLE(),
                            buf.ReadUnsignedShortLE(), buf.ReadUnsignedIntLE(), buf.ReadShortLE()));
                        break;
                    case 0x2A:
                    case 0x2B:
                    case 0x2C:
                    case 0x2D:
                    case 0x2E:
                    case 0x2F:
                    case 0x30:
                    case 0x31:
                        buf.ReadUnsignedByte(); // label
                        position.Set(Position.PrefixTemp + (id - 0x2A), buf.ReadShortLE() / 100.0);
                        break;
                    case 0x4B:
                        buf.SkipBytes(length); // network information
                        break;
                    case 0xFE31:
                        var alarmProtocol = buf.ReadUnsignedByte();
                        position.Set("alarmType", buf.ReadUnsignedByte());
                        if (alarmProtocol == 0x02 && length > 3)
                        {
                            var file = buf.ReadString(length - 2, Encoding.ASCII);
                            var folder = Regex.Replace(file[..8], @"(\d{4})(\d{2})(\d{2})", "$1-$2-$3");
                            position.Set(Position.KeyImage, folder + "/" + file);
                        }
                        else
                        {
                            buf.SkipBytes(length - 2);
                        }
                        break;
                    case 0xFE73:
                        buf.ReadUnsignedByte(); // version
                        position.Set("tagName", buf.ReadString(buf.ReadUnsignedByte(), Encoding.ASCII));
                        buf.SkipBytes(6); // mac
                        position.Set("tagBattery", buf.ReadUnsignedByte());
                        position.Set("tagTemp", buf.ReadShortLE() / 256.0);
                        position.Set("tagHumidity", buf.ReadShortLE() / 256.0);
                        buf.ReadUnsignedShortLE(); // high temperature threshold
                        buf.ReadUnsignedShortLE(); // low temperature threshold
                        buf.ReadUnsignedShortLE(); // high humidity threshold
                        buf.ReadUnsignedShortLE(); // low humidity threshold
                        break;
                    case 0xFEA8:
                        for (var k = 1; k <= 3; k++)
                        {
                            if (buf.ReadUnsignedByte() > 0)
                            {
                                var key = k == 1 ? Position.KeyBatteryLevel : $"battery{k}Level";
                                position.Set(key, buf.ReadUnsignedByte());
                            }
                            else
                            {
                                buf.ReadUnsignedByte();
                            }
                        }
                        buf.ReadUnsignedByte(); // battery alert
                        break;
                    default:
                        buf.SkipBytes(length);
                        break;
                }
            }

            buf.ReaderIndex = dataEnd;

            if (network.CellTowers != null || network.WifiAccessPoints != null)
            {
                position.Network = network;
            }
            positions.Add(position);
        }

        return positions;
    }

    private void RequestPhotoPacket(IChannel channel, EndPoint? remoteAddress, string imei, string file, int index)
    {
        var content = $"D00,{file},{index}";
        var length = 1 + imei.Length + 1 + content.Length + 5;
        var response = $"@@O{length:D2},{imei},{content}*";
        response += Checksum.Sum(response) + "\r\n";
        WriteResponse(channel, remoteAddress, Unpooled.CopiedBuffer(response, Encoding.ASCII));
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = (IByteBuffer)message;
        var wrapped = new ByteBuf(buf);

        var index = wrapped.IndexOf(wrapped.ReaderIndex, wrapped.WriterIndex, (byte)',');
        var imei = wrapped.ToString(index + 1, 15, Encoding.ASCII);
        index = wrapped.IndexOf(index + 1, wrapped.WriterIndex, (byte)',');
        var type = wrapped.ToString(index + 1, 3, Encoding.ASCII);

        switch (type)
        {
            case "AAC":
            {
                var response = $"@@z27,{imei},AAC,1*";
                response += Checksum.Sum(response) + "\r\n";
                WriteResponse(channel, remoteAddress, Unpooled.CopiedBuffer(response, Encoding.ASCII));
                return null;
            }
            case "D00":
            {
                if (GetMediaBuffer() == null)
                {
                    NewMediaBuffer();
                }

                index = index + 1 + type.Length + 1;
                var endIndex = wrapped.IndexOf(index, wrapped.WriterIndex, (byte)',');
                var file = wrapped.ToString(index, endIndex - index, Encoding.ASCII);
                index = endIndex + 1;
                endIndex = wrapped.IndexOf(index, wrapped.WriterIndex, (byte)',');
                var total = int.Parse(wrapped.ToString(index, endIndex - index, Encoding.ASCII));
                index = endIndex + 1;
                endIndex = wrapped.IndexOf(index, wrapped.WriterIndex, (byte)',');
                var current = int.Parse(wrapped.ToString(index, endIndex - index, Encoding.ASCII));

                wrapped.ReaderIndex = endIndex + 1;
                var chunk = new byte[wrapped.ReadableBytes - 1 - 2 - 2];
                wrapped.ReadBytes(chunk);
                GetMediaBuffer()!.WriteBytes(chunk);

                if (current == total - 1)
                {
                    var position = new Position(ProtocolName)
                    {
                        DeviceId = GetDeviceSession(channel, remoteAddress, imei)!.DeviceId,
                    };

                    GetLastLocation(position, null);

                    position.Set(Position.KeyImage, WriteMediaFile(imei, "jpg"));

                    return position;
                }
                else
                {
                    if ((current + 1) % 8 == 0)
                    {
                        RequestPhotoPacket(channel, remoteAddress, imei, file, current + 1);
                    }
                    return null;
                }
            }
            case "D03":
                NewMediaBuffer();
                RequestPhotoPacket(channel, remoteAddress, imei, "camera_picture.jpg", 0);
                return null;
            case "D82":
            {
                var position = new Position(ProtocolName)
                {
                    DeviceId = GetDeviceSession(channel, remoteAddress, imei)!.DeviceId,
                };
                GetLastLocation(position, null);
                var result = wrapped.ToString(index + 1, wrapped.WriterIndex - index - 4, Encoding.ASCII);
                position.Set(Position.KeyResult, result);
                return position;
            }
            case "CCC":
                return DecodeBinaryC(channel, remoteAddress, wrapped);
            case "CCE":
                return DecodeBinaryE(channel, remoteAddress, wrapped);
            default:
                return DecodeRegular(channel, remoteAddress, buf);
        }
    }
}
