using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.H02;

public sealed class H02ProtocolDecoder(ConnectionManager connectionManager, ILogger<H02ProtocolDecoder> logger)
    : BaseProtocolDecoder("h02", connectionManager, logger)
{
    private static double ReadCoordinate(ByteBuf buf, bool lon)
    {
        var degrees = BcdUtil.ReadInteger(buf, 2);
        if (lon)
        {
            degrees = degrees * 10 + (buf.GetUnsignedByte(buf.ReaderIndex) >> 4);
        }

        double result = 0;
        if (lon)
        {
            result = buf.ReadUnsignedByte() & 0x0F;
        }

        var length = lon ? 5 : 6;
        result = result * 10 + BcdUtil.ReadInteger(buf, length) / 10000.0;

        result /= 60;
        result += degrees;

        return result;
    }

    private static void ProcessStatus(Position position, long status)
    {
        if (!BitUtil.Check(status, 0))
        {
            position.AddAlarm(Position.AlarmVibration);
        }
        else if (!BitUtil.Check(status, 1) || !BitUtil.Check(status, 18))
        {
            position.AddAlarm(Position.AlarmSos);
        }
        else if (!BitUtil.Check(status, 2))
        {
            position.AddAlarm(Position.AlarmOverspeed);
        }
        else if (!BitUtil.Check(status, 19))
        {
            position.AddAlarm(Position.AlarmPowerCut);
        }

        position.Set(Position.KeyIgnition, BitUtil.Check(status, 10));
        position.Set(Position.KeyStatus, status);
    }

    private static int? DecodeBattery(int value)
    {
        return value switch
        {
            0 => null,
            <= 3 => (value - 1) * 10,
            <= 6 => (value - 1) * 20,
            <= 100 => value,
            >= 0xF1 and <= 0xF6 => value - 0xF0,
            _ => null,
        };
    }

    private Position? DecodeBinary(ByteBuf buf, IChannel channel, EndPoint? remoteAddress)
    {
        var position = new Position(ProtocolName);

        var longId = buf.ReadableBytes == 42;

        buf.ReadByte(); // marker

        string id;
        if (longId)
        {
            id = ByteBufferUtil.HexDump(buf.ReadSlice(8))[..15];
        }
        else
        {
            id = ByteBufferUtil.HexDump(buf.ReadSlice(5));
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, id);
        if (deviceSession == null)
        {
            return null;
        }
        position.DeviceId = deviceSession.DeviceId;

        var dateBuilder = new DateBuilder()
            .SetHour(BcdUtil.ReadInteger(buf, 2))
            .SetMinute(BcdUtil.ReadInteger(buf, 2))
            .SetSecond(BcdUtil.ReadInteger(buf, 2))
            .SetDay(BcdUtil.ReadInteger(buf, 2))
            .SetMonth(BcdUtil.ReadInteger(buf, 2))
            .SetYear(BcdUtil.ReadInteger(buf, 2));
        position.SetTime(dateBuilder.GetDate());

        var latitude = ReadCoordinate(buf, false);
        position.Set(Position.KeyBatteryLevel, DecodeBattery(buf.ReadUnsignedByte()));
        var longitude = ReadCoordinate(buf, true);

        var flags = buf.ReadUnsignedByte() & 0x0F;
        position.Valid = (flags & 0x02) != 0;
        if ((flags & 0x04) == 0)
        {
            latitude = -latitude;
        }
        if ((flags & 0x08) == 0)
        {
            longitude = -longitude;
        }

        position.Latitude = latitude;
        position.Longitude = longitude;

        position.Speed = BcdUtil.ReadInteger(buf, 3);
        position.Course = (buf.ReadUnsignedByte() & 0x0F) * 100.0 + BcdUtil.ReadInteger(buf, 2);

        ProcessStatus(position, buf.ReadUnsignedInt());

        return position;
    }

    private static readonly Regex Pattern = Parser.Compile(
        @"\*..,(\d+)?,(?:V4,(.*),|(V[^,]*),)(?:(\d{2})(\d{2})(\d{2}))?,(?:([ABV])?,|(\d+),)" +
        @"(?:-(\d+)-(\d+\.\d+),([NS]),|(\d*)(\d{2}\.\d+),([NS]),|(\d+)(\d{2})(\d{4}),([NS]),)" +
        @"(?:-(\d+)-(\d+\.\d+),([EW]),|(\d*)(\d{2}\.\d+),([EW]),|(\d+)(\d{2})(\d{4}),([EW]),)" +
        @" *(\d+\.?\d*),(\d+\.?\d*)?,(?:\d+,)?(?:(\d{2})(\d{2})(\d{2}))?(?:,[^,]*,[^,]*,[^,]*)?" +
        @"(?:,([0-9a-fA-F]{8})(?:,(\d+),(-?\d+),(\d+\.\d+),(-?\d+),([0-9a-fA-F]+),([0-9a-fA-F]+)|,(.*)|)|)#");

    private static readonly Regex PatternNbr = Parser.Compile(
        @"\*..,(\d+),NBR,(\d{2})(\d{2})(\d{2}),(\d+),(\d+),\d+,\d+,((?:\d+,\d+,-?\d+,)+)(\d{2})(\d{2})(\d{2}),([0-9a-fA-F]{8}).*");

    private static readonly Regex PatternLink = Parser.Compile(
        @"\*..,(\d+),LINK,(\d{2})(\d{2})(\d{2}),(\d+),(\d+),(\d+),(\d+),(\d+),(\d{2})(\d{2})(\d{2}),([0-9a-fA-F]{8}).*");

    private static readonly Regex PatternV3 = Parser.Compile(
        @"\*..,(\d+),V3,(\d{2})(\d{2})(\d{2}),(\d{3})(\d+),(\d+),(.*),([0-9a-fA-F]{4}),\d+,X,(\d{2})(\d{2})(\d{2}),([0-9a-fA-F]{8})#?");

    private static readonly Regex PatternVp1 = Parser.Compile(
        @"\*hq,(\d{15}),VP1,(?:V,(\d+),(\d+),([^#]+)|[AB],(\d+)(\d{2}\.\d+),([NS]),(\d+)(\d{2}\.\d+),([EW]),(\d+\.\d+),(\d+\.\d+),(\d{2})(\d{2})(\d{2})).*");

    private static readonly Regex PatternHtbt = Parser.Compile(@"\*HQ,(\d{15}),HTBT,(\d+).*");

    private static readonly Regex PatternSms = Parser.Compile(@"\*HQ,(\d+),SMS,(.+)#");

    private void SendResponse(IChannel channel, EndPoint? remoteAddress, string? id, string type)
    {
        if (channel == null || id == null)
        {
            return;
        }
        var time = type == "R12"
            ? DateTime.UtcNow.ToString("HHmmss")
            : DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var response = type == "R12"
            ? $"*HQ,{id},{type},{time}#"
            : $"*HQ,{id},V4,{type},{time}#";
        WriteResponse(channel, remoteAddress, Unpooled.CopiedBuffer(response, Encoding.ASCII));
    }

    private Position? DecodeText(string sentence, IChannel channel, EndPoint? remoteAddress)
    {
        var parser = new Parser(Pattern, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var id = parser.Next();
        var deviceSession = GetDeviceSession(channel, remoteAddress, id);
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        if (parser.HasNext())
        {
            position.Set(Position.KeyResult, parser.Next());
        }

        if (parser.HasNext() && parser.Next() == "V1")
        {
            SendResponse(channel, remoteAddress, id, "V1");
        }

        var dateBuilder = new DateBuilder();
        if (parser.HasNext(3))
        {
            dateBuilder.SetTime(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));
        }

        if (parser.HasNext())
        {
            position.Valid = parser.Next() == "A";
        }
        if (parser.HasNext())
        {
            parser.NextInt(); // coding scheme
            position.Valid = true;
        }

        if (parser.HasNext(3))
        {
            position.Latitude = parser.NextCoordinate();
        }
        if (parser.HasNextAny(3))
        {
            position.Latitude = parser.NextCoordinate();
        }
        if (parser.HasNext(4))
        {
            position.Latitude = parser.NextCoordinate(CoordinateFormat.DegMinMinHem);
        }

        if (parser.HasNext(3))
        {
            position.Longitude = parser.NextCoordinate();
        }
        if (parser.HasNextAny(3))
        {
            position.Longitude = parser.NextCoordinate();
        }
        if (parser.HasNext(4))
        {
            position.Longitude = parser.NextCoordinate(CoordinateFormat.DegMinMinHem);
        }

        position.Speed = parser.NextDouble(0);
        position.Course = parser.NextDouble(0);

        if (parser.HasNext(3))
        {
            dateBuilder.SetDateReverse(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));
            position.SetTime(dateBuilder.GetDate());
        }
        else
        {
            position.SetTime(DateTime.UtcNow);
        }

        if (parser.HasNext())
        {
            ProcessStatus(position, parser.NextLong(16, 0));
        }

        if (parser.HasNext(6))
        {
            position.Set(Position.KeyOdometer, parser.NextInt(0));
            position.Set(Position.PrefixTemp + 1, parser.NextInt(0));
            position.Set(Position.KeyFuel, parser.NextDouble(0));

            position.Altitude = parser.NextInt(0);

            var network = new Network();
            network.AddCellTower(CellTower.From(0, 0, parser.NextHexInt(0), parser.NextHexInt(0)));
            position.Network = network;
        }

        if (parser.HasNext())
        {
            var values = parser.Next()!.Split(',');
            for (var i = 0; i < values.Length; i++)
            {
                position.Set(Position.PrefixIo + (i + 1), values[i].Trim());
            }
        }

        return position;
    }

    private Position? DecodeLbs(string sentence, IChannel channel, EndPoint? remoteAddress)
    {
        var parser = new Parser(PatternNbr, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var id = parser.Next();
        var deviceSession = GetDeviceSession(channel, remoteAddress, id);
        if (deviceSession == null)
        {
            return null;
        }

        SendResponse(channel, remoteAddress, id, "NBR");

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var dateBuilder = new DateBuilder()
            .SetTime(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

        var network = new Network();
        var mcc = parser.NextInt(0);
        var mnc = parser.NextInt(0);

        var cells = parser.Next()!.TrimEnd(',').Split(',');
        for (var i = 0; i < cells.Length / 3; i++)
        {
            network.AddCellTower(CellTower.From(
                mcc, mnc, int.Parse(cells[i * 3]), int.Parse(cells[i * 3 + 1]), int.Parse(cells[i * 3 + 2])));
        }

        position.Network = network;

        dateBuilder.SetDateReverse(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

        GetLastLocation(position, dateBuilder.GetDate());

        ProcessStatus(position, parser.NextLong(16, 0));

        return position;
    }

    private Position? DecodeLink(string sentence, IChannel channel, EndPoint? remoteAddress)
    {
        var parser = new Parser(PatternLink, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var dateBuilder = new DateBuilder()
            .SetTime(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

        position.Set(Position.KeyRssi, parser.NextInt());
        position.Set(Position.KeySatellites, parser.NextInt());
        position.Set(Position.KeyBatteryLevel, parser.NextInt());
        position.Set(Position.KeySteps, parser.NextInt());
        position.Set("turnovers", parser.NextInt());

        dateBuilder.SetDateReverse(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

        GetLastLocation(position, dateBuilder.GetDate());

        ProcessStatus(position, parser.NextLong(16, 0));

        return position;
    }

    private Position? DecodeV3(string sentence, IChannel channel, EndPoint? remoteAddress)
    {
        var parser = new Parser(PatternV3, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var dateBuilder = new DateBuilder()
            .SetTime(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

        var mcc = parser.NextInt();
        var mnc = parser.NextInt();

        var count = parser.NextInt() ?? 0;
        var network = new Network();
        var values = parser.Next()!.TrimEnd(',').Split(',');
        for (var i = 0; i < count && (i * 4 + 1) < values.Length; i++)
        {
            network.AddCellTower(CellTower.From(
                mcc ?? 0, mnc ?? 0, int.Parse(values[i * 4]), int.Parse(values[i * 4 + 1])));
        }
        position.Network = network;

        position.Set(Position.KeyBattery, parser.NextHexInt());

        dateBuilder.SetDateReverse(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

        GetLastLocation(position, dateBuilder.GetDate());

        ProcessStatus(position, parser.NextLong(16, 0));

        return position;
    }

    private Position? DecodeSms(string sentence, IChannel channel, EndPoint? remoteAddress)
    {
        var parser = new Parser(PatternSms, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        GetLastLocation(position, null);

        position.Set(Position.KeyResult, parser.Next());

        return position;
    }

    private Position? DecodeVp1(string sentence, IChannel channel, EndPoint? remoteAddress)
    {
        var parser = new Parser(PatternVp1, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        if (parser.HasNext(3))
        {
            GetLastLocation(position, null);

            var mcc = parser.NextInt(0);
            var mnc = parser.NextInt(0);

            var network = new Network();
            foreach (var cell in parser.Next()!.Split('Y'))
            {
                if (cell.Length == 0)
                {
                    continue;
                }
                var values = cell.Split(',');
                network.AddCellTower(CellTower.From(
                    mcc, mnc, int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2])));
            }

            position.Network = network;
        }
        else
        {
            position.Valid = true;
            position.Latitude = parser.NextCoordinate();
            position.Longitude = parser.NextCoordinate();
            position.Speed = parser.NextDouble(0);
            position.Course = parser.NextDouble(0);

            position.SetTime(new DateBuilder()
                .SetDateReverse(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0)).GetDate());
        }

        return position;
    }

    private Position? DecodeHeartbeat(string sentence, IChannel channel, EndPoint? remoteAddress)
    {
        var parser = new Parser(PatternHtbt, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var deviceSession = GetDeviceSession(channel, remoteAddress, parser.Next());
        if (deviceSession == null)
        {
            return null;
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        GetLastLocation(position, null);

        position.Set(Position.KeyBatteryLevel, parser.NextInt());

        return position;
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = new ByteBuf((IByteBuffer)message);
        var marker = (char)buf.GetByte(buf.ReaderIndex);

        switch (marker)
        {
            case '*':
                var sentence = buf.ToString(Encoding.ASCII).TrimEnd('\r', '\n');
                var typeStart = sentence.IndexOf(',', sentence.IndexOf(',') + 1) + 1;
                var typeEnd = sentence.IndexOf(',', typeStart);
                if (typeEnd < 0)
                {
                    typeEnd = sentence.IndexOf('#', typeStart);
                }
                if (typeEnd <= 0)
                {
                    return null;
                }

                var type = sentence[typeStart..typeEnd];
                switch (type)
                {
                    case "V0" or "HTBT":
                        var response = sentence[..typeEnd] + "#";
                        WriteResponse(channel, remoteAddress, Unpooled.CopiedBuffer(response, Encoding.ASCII));
                        return DecodeHeartbeat(sentence, channel, remoteAddress);
                    case "NBR":
                        return DecodeLbs(sentence, channel, remoteAddress);
                    case "LINK":
                        return DecodeLink(sentence, channel, remoteAddress);
                    case "V3":
                        return DecodeV3(sentence, channel, remoteAddress);
                    case "VP1":
                        return DecodeVp1(sentence, channel, remoteAddress);
                    case "SMS":
                        return DecodeSms(sentence, channel, remoteAddress);
                    default:
                        return DecodeText(sentence, channel, remoteAddress);
                }
            case '$':
                return DecodeBinary(buf, channel, remoteAddress);
            default:
                return null;
        }
    }
}
