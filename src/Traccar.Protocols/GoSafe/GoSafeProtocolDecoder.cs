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

namespace Traccar.Protocols.GoSafe;

public sealed class GoSafeProtocolDecoder(ConnectionManager connectionManager, ILogger<GoSafeProtocolDecoder> logger)
    : BaseProtocolDecoder("gosafe", connectionManager, logger)
{
    private static readonly Regex Pattern = Parser.Compile(@"\*GS\d+,(\d+),([^#]*)#?");

    private static readonly Regex PatternOld = Parser.Compile(
        @"\*GS\d+,(\d+),GPS:(\d{2})(\d{2})(\d{2});(?:\d;)?([AV]);([NS])(\d+\.\d+);([EW])(\d+\.\d+);(\d+)?;(\d+);(\d+\.?\d*)?(\d{2})(\d{2})(\d{2}).*");

    private static readonly Regex DigitsOnly = new(@"^\d{10}[1-9]\d$", RegexOptions.Compiled);

    private static readonly Regex HexOnly = new(@"^[0-9A-Fa-f]+$", RegexOptions.Compiled);

    private static void DecodeTextFragment(Position position, string fragment)
    {
        var dataIndex = fragment.IndexOf(':');
        var index = 0;
        var values = fragment.Length == dataIndex + 1
            ? []
            : JavaString.Split(fragment[(dataIndex + 1)..], ';');

        switch (fragment[..dataIndex])
        {
            case "GPS":
                position.Valid = values[index++] == "A";
                position.Set(Position.KeySatellites, int.Parse(values[index++]));
                position.Latitude = double.Parse(values[index][1..], CultureInfo.InvariantCulture);
                if (values[index++][0] == 'S')
                {
                    position.Latitude = -position.Latitude;
                }
                position.Longitude = double.Parse(values[index][1..], CultureInfo.InvariantCulture);
                if (values[index++][0] == 'W')
                {
                    position.Longitude = -position.Longitude;
                }
                if (values[index++].Length > 0)
                {
                    position.Speed = UnitsConverter.KnotsFromKph(int.Parse(values[index - 1]));
                }
                position.Course = int.Parse(values[index++]);
                if (index < values.Length && values[index++].Length > 0)
                {
                    position.Altitude = int.Parse(values[index - 1]);
                }
                if (index < values.Length && values[index++].Length > 0)
                {
                    position.Set(Position.KeyHdop, double.Parse(values[index - 1], CultureInfo.InvariantCulture));
                }
                if (index < values.Length && values[index++].Length > 0)
                {
                    position.Set(Position.KeyVdop, double.Parse(values[index - 1], CultureInfo.InvariantCulture));
                }
                break;
            case "GSM":
                index += 1; // registration status
                index += 1; // signal strength
                var network = new Network();
                network.AddCellTower(CellTower.From(
                    int.Parse(values[index++]), int.Parse(values[index++]),
                    Convert.ToInt32(values[index++], 16), Convert.ToInt32(values[index++], 16),
                    int.Parse(values[index++])));
                position.Network = network;
                break;
            case "COT":
                if (index < values.Length)
                {
                    position.Set(Position.KeyOdometer, long.Parse(values[index++]));
                }
                if (index < values.Length)
                {
                    var hours = JavaString.Split(values[index], '-');
                    position.Set(Position.KeyHours, (int.Parse(hours[0]) * 3600
                        + (hours.Length > 1 ? int.Parse(hours[1]) * 60 : 0)
                        + (hours.Length > 2 ? int.Parse(hours[2]) : 0)) * 1000);
                }
                break;
            case "ADC":
                position.Set(Position.KeyPower, double.Parse(values[index++], CultureInfo.InvariantCulture));
                if (index < values.Length)
                {
                    position.Set(Position.KeyBattery, double.Parse(values[index++], CultureInfo.InvariantCulture));
                }
                if (index < values.Length)
                {
                    position.Set(Position.PrefixAdc + 1, double.Parse(values[index++], CultureInfo.InvariantCulture));
                }
                if (index < values.Length)
                {
                    position.Set(Position.PrefixAdc + 2, double.Parse(values[index++], CultureInfo.InvariantCulture));
                }
                break;
            case "DTT":
                position.Set(Position.KeyStatus, Convert.ToInt32(values[index++], 16));
                if (values[index++].Length > 0)
                {
                    var io = Convert.ToInt32(values[index - 1], 16);
                    position.Set(Position.KeyIgnition, BitUtil.Check(io, 0));
                    position.Set(Position.PrefixIn + 1, BitUtil.Check(io, 1));
                    position.Set(Position.PrefixIn + 2, BitUtil.Check(io, 2));
                    position.Set(Position.PrefixIn + 3, BitUtil.Check(io, 3));
                    position.Set(Position.PrefixIn + 4, BitUtil.Check(io, 4));
                    position.Set(Position.PrefixOut + 1, BitUtil.Check(io, 5));
                    position.Set(Position.PrefixOut + 2, BitUtil.Check(io, 6));
                    position.Set(Position.PrefixOut + 3, BitUtil.Check(io, 7));
                }
                position.Set(Position.KeyGeofence, values[index++] + values[index++]);
                position.Set("eventStatus", values[index++]);
                if (index < values.Length)
                {
                    position.Set("packetType", values[index++]);
                }
                break;
            case "ETD":
                position.Set("eventData", values[index++]);
                break;
            case "OBD":
                position.Set("obd", values[index++]);
                break;
            case "TAG":
                position.Set("tagData", values[index++]);
                break;
            case "IWD":
                while (index < values.Length)
                {
                    var sensorIndex = int.Parse(values[index++]);
                    var dataType = int.Parse(values[index++]);
                    if (dataType == 0)
                    {
                        position.Set(Position.KeyDriverUniqueId, values[index++]);
                    }
                    else if (dataType == 1)
                    {
                        index += 1; // temperature sensor serial number
                        position.Set(Position.PrefixTemp + sensorIndex, double.Parse(values[index++], CultureInfo.InvariantCulture));
                    }
                }
                break;
        }
    }

    private Position DecodeTextPosition(DeviceSession deviceSession, string sentence)
    {
        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var index = 0;
        var fragments = JavaString.Split(sentence, ',');

        if (DigitsOnly.IsMatch(fragments[index]))
        {
            position.SetTime(DateTime.ParseExact(
                fragments[index++], "HHmmssddMMyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
        }
        else
        {
            GetLastLocation(position, null);
            position.Set(Position.KeyResult, fragments[index++]);
        }

        for (; index < fragments.Length; index++)
        {
            if (fragments[index].Length == 0)
            {
                continue;
            }
            if (HexOnly.IsMatch(fragments[index]))
            {
                position.Set(Position.KeyEvent, Convert.ToInt32(fragments[index], 16));
            }
            else
            {
                DecodeTextFragment(position, fragments[index]);
            }
        }

        return position;
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        WriteResponse(channel, remoteAddress, Unpooled.CopiedBuffer("1234", Encoding.ASCII));

        var buf = new ByteBuf((IByteBuffer)message);
        var marker = (char)buf.GetByte(buf.ReaderIndex);
        return marker == '*'
            ? DecodeText(channel, remoteAddress, buf.ToString(Encoding.ASCII))
            : DecodeBinary(channel, remoteAddress, buf);
    }

    private object? DecodeText(IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var old = sentence.StartsWith("*GS02");
        var pattern = old ? PatternOld : Pattern;

        var parser = new Parser(pattern, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        if (old)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            var dateBuilder = new DateBuilder()
                .SetTime(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

            position.Valid = parser.Next() == "A";
            position.Latitude = parser.NextCoordinate(CoordinateFormat.HemDeg);
            position.Longitude = parser.NextCoordinate(CoordinateFormat.HemDeg);
            position.Speed = UnitsConverter.KnotsFromKph(parser.NextDouble(0));
            position.Course = parser.NextDouble(0);

            position.Set(Position.KeyHdop, parser.Next());

            dateBuilder.SetDateReverse(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));
            position.SetTime(dateBuilder.GetDate());

            return position;
        }

        var positions = new List<Position>();
        foreach (var item in JavaString.Split(parser.Next()!, '$'))
        {
            positions.Add(DecodeTextPosition(deviceSession, item));
        }
        return positions;
    }

    private object? DecodeBinary(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        buf.ReadUnsignedByte(); // header
        buf.ReadUnsignedByte(); // protocol version
        var type = buf.ReadUnsignedByte();

        var imei = ((buf.ReadUnsignedInt() << (3 * 8)) | (long)buf.ReadUnsignedMedium()).ToString();
        var deviceSession = GetDeviceSession(channel, remoteAddress, imei);
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var seconds = buf.ReadUnsignedInt() + 946684800L; // from 2000-01-01
        position.SetTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime);

        if (type == 0x41)
        {
            buf.ReadUnsignedByte(); // event id
        }

        var mask = buf.ReadUnsignedShort();

        if (BitUtil.Check(mask, 0))
        {
            buf.SkipBytes(buf.ReadUnsignedByte()); // SYS
        }

        if (BitUtil.Check(mask, 1))
        {
            buf.ReadUnsignedByte(); // length
            var fragmentMask = buf.ReadUnsignedShort();

            if (BitUtil.Check(fragmentMask, 0))
            {
                var flags = buf.ReadUnsignedByte();
                position.Valid = BitUtil.Between(flags, 5, 7) > 0;
                position.Set(Position.KeySatellites, BitUtil.To(flags, 5));
            }

            if (BitUtil.Check(fragmentMask, 1))
            {
                position.Latitude = buf.ReadInt() / 1000000.0;
                position.Longitude = buf.ReadInt() / 1000000.0;
            }

            if (BitUtil.Check(fragmentMask, 2))
            {
                position.Speed = UnitsConverter.KnotsFromKph(buf.ReadShort());
            }

            if (BitUtil.Check(fragmentMask, 3))
            {
                position.Course = buf.ReadUnsignedShort();
            }

            if (BitUtil.Check(fragmentMask, 4))
            {
                position.Altitude = buf.ReadShort();
            }

            if (BitUtil.Check(fragmentMask, 5))
            {
                position.Set(Position.KeyHdop, buf.ReadUnsignedShort() / 100.0);
            }

            if (BitUtil.Check(fragmentMask, 6))
            {
                position.Set(Position.KeyVdop, buf.ReadUnsignedShort() / 100.0);
            }
        }
        else
        {
            GetLastLocation(position, position.DeviceTime);
        }

        if (BitUtil.Check(mask, 2))
        {
            buf.SkipBytes(buf.ReadUnsignedByte()); // GSM
        }

        if (BitUtil.Check(mask, 3))
        {
            buf.SkipBytes(buf.ReadUnsignedByte()); // COT
        }

        if (BitUtil.Check(mask, 4))
        {
            buf.SkipBytes(buf.ReadUnsignedByte()); // ADC
        }

        if (BitUtil.Check(mask, 5))
        {
            buf.SkipBytes(buf.ReadUnsignedByte()); // DTT
        }

        if (BitUtil.Check(mask, 6))
        {
            buf.SkipBytes(buf.ReadUnsignedByte()); // IWD
        }

        if (BitUtil.Check(mask, 7))
        {
            buf.SkipBytes(buf.ReadUnsignedByte()); // ETD
        }

        return position;
    }
}
