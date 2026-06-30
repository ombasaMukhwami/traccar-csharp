using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Meiligao;

public sealed class MeiligaoProtocolDecoder(ConnectionManager connectionManager, ILogger<MeiligaoProtocolDecoder> logger)
    : BaseProtocolDecoder("meiligao", connectionManager, logger)
{
    private static readonly Regex Pattern = Parser.Compile(
        @"(\d+)(\d{2})(\d{2})\.?\d*," +                 // time (hhmmss)
        @"([AV])," +                                     // validity
        @"(\d+)(\d{2}\.\d+)," +                          // latitude
        @"([NS])," +
        @"(\d+)(\d{2}\.\d+)," +                          // longitude
        @"([EW])," +
        @"(\d+\.?\d*)?," +                                // speed
        @"(\d+\.?\d*)?," +                                // course
        @"(\d{2})(\d{2})(\d{2})" +                       // date (ddmmyy)
        @"[^\|]*" +
        @"(?:" +
        @"\|(\d+\.\d+)?" +                                // hdop
        @"\|(-?\d+\.?\d*)?" +                             // altitude
        @"\|([0-9a-fA-F]{4})?" +                          // state
        @"(?:" +
        @"\|([0-9a-fA-F]{4}),([0-9a-fA-F]{4})" +          // adc
        @"(?:,([0-9a-fA-F]{4}))?" +
        @"(?:,([0-9a-fA-F]{4}))?" +
        @"(?:,([0-9a-fA-F]{4}))?" +
        @"(?:,([0-9a-fA-F]{4}))?" +
        @"(?:,([0-9a-fA-F]{4}))?" +
        @"(?:,([0-9a-fA-F]{4}))?" +
        @"(?:" +
        @"\|[0-9a-fA-F]{16,20}" +                         // cell
        @"\|([0-9a-fA-F]{2})" +                           // rssi
        @"\|([0-9a-fA-F]{8})" +                           // odometer
        @"(?:" +
        @"\|([0-9a-fA-F]{2})" +                           // satellites
        @"(?:" +
        @"\|" +
        @"(.*)" +                                          // driver
        @")?" +
        @")?" +
        @"|" +
        @"\|(\d{1,9})" +                                  // odometer
        @"(?:" +
        @"\|([0-9a-fA-F]{5,})" +                          // rfid
        @")?" +
        @")?" +
        @")?" +
        @")?" +
        @".*");

    private static readonly Regex PatternRfid = Parser.Compile(
        @"\|(\d{2})(\d{2})(\d{2})," +                     // time (hhmmss)
        @"(\d{2})(\d{2})(\d{2})," +                       // date (ddmmyy)
        @"(\d+)(\d{2}\.\d+)," +                           // latitude
        @"([NS])," +
        @"(\d+)(\d{2}\.\d+)," +                           // longitude
        @"([EW])");

    private static readonly Regex PatternObd = Parser.Compile(
        @"(\d+\.\d+)," +                                  // battery
        @"(\d+)," +                                       // rpm
        @"(\d+)," +                                       // speed
        @"(\d+\.\d+)," +                                  // throttle
        @"(\d+\.\d+)," +                                  // engine load
        @"(-?\d+)," +                                     // coolant temp
        @"(\d+\.\d+)," +                                  // instantaneous fuel
        @"(\d+\.\d+)," +                                  // average fuel
        @"(\d+\.\d+)," +                                  // driving range
        @"(\d+\.?\d*)," +                                 // odometer
        @"(\d+\.\d+)," +                                  // single fuel consumption
        @"(\d+\.\d+)," +                                  // total fuel consumption
        @"(\d+)," +                                       // error code count
        @"(\d+)," +                                       // hard acceleration count
        @"(\d+)");                                        // hard brake count

    private static readonly Regex PatternObdA = Parser.Compile(
        @"(\d+)," +                                       // total ignition
        @"(\d+\.\d+)," +                                  // total driving time
        @"(\d+\.\d+)," +                                  // total idling time
        @"(\d+)," +                                       // average hot start time
        @"(\d+)," +                                       // average speed
        @"(\d+)," +                                       // history highest speed
        @"(\d+)," +                                       // history highest rpm
        @"(\d+)," +                                       // total hard acceleration
        @"(\d+)");                                        // total hard brake

    public const int MsgHeartbeat = 0x0001;
    public const int MsgServer = 0x0002;
    public const int MsgLogin = 0x5000;
    public const int MsgLoginResponse = 0x4000;
    public const int MsgPosition = 0x9955;
    public const int MsgPositionLogged = 0x9016;
    public const int MsgAlarm = 0x9999;
    public const int MsgRfid = 0x9966;
    public const int MsgRetransmission = 0x6688;

    public const int MsgObdRt = 0x9901;
    public const int MsgObdRta = 0x9902;
    public const int MsgDtc = 0x9903;

    public const int MsgTrackOnDemand = 0x4101;
    public const int MsgTrackByInterval = 0x4102;
    public const int MsgMovementAlarm = 0x4106;
    public const int MsgOutputControl1 = 0x4114;
    public const int MsgOutputControl2 = 0x4115;
    public const int MsgTimeZone = 0x4132;
    public const int MsgTakePhoto = 0x4151;
    public const int MsgUploadPhoto = 0x0800;
    public const int MsgUploadPhotoResponse = 0x8801;
    public const int MsgDataPhoto = 0x9988;
    public const int MsgPositionImage = 0x9977;
    public const int MsgUploadComplete = 0x0f80;
    public const int MsgRebootGps = 0x4902;

    private DeviceSession? Identify(ByteBuf buf, IChannel channel, EndPoint? remoteAddress)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < 7; i++)
        {
            var b = buf.ReadUnsignedByte();

            // First digit
            var d1 = (b & 0xf0) >> 4;
            if (d1 == 0xf)
            {
                break;
            }
            builder.Append(d1);

            // Second digit
            var d2 = b & 0x0f;
            if (d2 == 0xf)
            {
                break;
            }
            builder.Append(d2);
        }

        var id = builder.ToString();

        if (id.Length == 14)
        {
            return GetDeviceSession(channel, remoteAddress, id, id + Checksum.Luhn(long.Parse(id)));
        }
        return GetDeviceSession(channel, remoteAddress, id);
    }

    private static void SendResponse(IChannel channel, EndPoint? remoteAddress, IByteBuffer id, int type, IByteBuffer msg)
    {
        var buf = Unpooled.Buffer(2 + 2 + id.ReadableBytes + 2 + msg.ReadableBytes + 2 + 2);

        buf.WriteByte('@');
        buf.WriteByte('@');
        buf.WriteShort(buf.Capacity);
        buf.WriteBytes(id);
        buf.WriteShort(type);
        buf.WriteBytes(msg);

        var checksumBytes = new byte[buf.ReadableBytes];
        buf.GetBytes(buf.ReaderIndex, checksumBytes);
        buf.WriteShort(Checksum.Crc16(Checksum.Crc16CcittFalse, checksumBytes));
        buf.WriteByte('\r');
        buf.WriteByte('\n');

        WriteResponse(channel, remoteAddress, buf);
    }

    private static string? DecodeAlarm(string? model, int value)
    {
        if (model == "TK218")
        {
            return value switch
            {
                0x01 => Position.AlarmSos,
                0x10 => Position.AlarmLowBattery,
                0x11 => Position.AlarmOverspeed,
                0x12 => Position.AlarmMovement,
                0x13 => Position.AlarmGeofence,
                0x60 => Position.AlarmFatigueDriving,
                0x71 => Position.AlarmBraking,
                0x72 => Position.AlarmAcceleration,
                0x73 => Position.AlarmAccident,
                0x74 => Position.AlarmIdle,
                _ => null,
            };
        }
        return value switch
        {
            0x01 => Position.AlarmSos,
            0x10 => Position.AlarmLowBattery,
            0x11 => Position.AlarmOverspeed,
            0x12 => Position.AlarmMovement,
            0x13 => Position.AlarmGeofenceEnter,
            0x14 => Position.AlarmAccident,
            0x50 => Position.AlarmPowerOff,
            0x53 => Position.AlarmGpsAntennaCut,
            0x72 => Position.AlarmBraking,
            0x73 => Position.AlarmAcceleration,
            _ => null,
        };
    }

    private static Position? DecodeRegular(Position position, string sentence)
    {
        var parser = new Parser(Pattern, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        var dateBuilder = new DateBuilder().SetTime(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));

        position.Valid = parser.Next() == "A";
        position.Latitude = parser.NextCoordinate();
        position.Longitude = parser.NextCoordinate();

        if (parser.HasNext())
        {
            position.Speed = parser.NextDouble(0);
        }

        if (parser.HasNext())
        {
            position.Course = parser.NextDouble(0);
        }

        dateBuilder.SetDateReverse(parser.NextInt(0), parser.NextInt(0), parser.NextInt(0));
        position.SetTime(dateBuilder.GetDate());

        position.Set(Position.KeyHdop, parser.NextDouble());

        if (parser.HasNext())
        {
            position.Altitude = parser.NextDouble(0);
        }

        if (parser.HasNext())
        {
            var status = parser.NextHexInt()!.Value;
            for (var i = 1; i <= 5; i++)
            {
                position.Set(Position.PrefixOut + i, BitUtil.Check(status, i - 1));
            }
            for (var i = 1; i <= 5; i++)
            {
                position.Set(Position.PrefixIn + i, BitUtil.Check(status, i - 1 + 8));
            }
        }

        for (var i = 1; i <= 8; i++)
        {
            position.Set(Position.PrefixAdc + i, parser.NextHexInt());
        }

        position.Set(Position.KeyRssi, parser.NextHexInt());
        position.Set(Position.KeyOdometer, parser.NextHexLong());
        position.Set(Position.KeySatellites, parser.NextHexInt());
        position.Set(Position.KeyCard, parser.Next());
        position.Set(Position.KeyOdometer, parser.NextLong());
        position.Set(Position.KeyDriverUniqueId, parser.Next());

        return position;
    }

    private static Position? DecodeRfid(Position position, string sentence)
    {
        var parser = new Parser(PatternRfid, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        position.SetTime(parser.NextDateTime(DateTimeFormat.HmsDmy));

        position.Valid = true;
        position.Latitude = parser.NextCoordinate();
        position.Longitude = parser.NextCoordinate();

        return position;
    }

    private Position? DecodeObd(Position position, string sentence)
    {
        var parser = new Parser(PatternObd, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        GetLastLocation(position, null);

        position.Set(Position.KeyBattery, parser.NextDouble());
        position.Set(Position.KeyRpm, parser.NextInt());
        position.Set(Position.KeyObdSpeed, parser.NextInt());
        position.Set(Position.KeyThrottle, parser.NextDouble());
        position.Set(Position.KeyEngineLoad, parser.NextDouble());
        position.Set(Position.KeyCoolantTemp, parser.NextInt());
        position.Set(Position.KeyFuelConsumption, parser.NextDouble());
        position.Set("averageFuelConsumption", parser.NextDouble());
        position.Set("drivingRange", parser.NextDouble());
        position.Set(Position.KeyOdometer, parser.NextDouble());
        position.Set("singleFuelConsumption", parser.NextDouble());
        position.Set(Position.KeyFuelUsed, parser.NextDouble());
        position.Set(Position.KeyDtcs, parser.NextInt());
        position.Set("hardAccelerationCount", parser.NextInt());
        position.Set("hardBrakingCount", parser.NextInt());

        return position;
    }

    private Position? DecodeObdA(Position position, string sentence)
    {
        var parser = new Parser(PatternObdA, sentence);
        if (!parser.Matches())
        {
            return null;
        }

        GetLastLocation(position, null);

        position.Set("totalIgnitionNo", parser.NextInt(0));
        position.Set("totalDrivingTime", parser.NextDouble(0));
        position.Set("totalIdlingTime", parser.NextDouble(0));
        position.Set("averageHotStartTime", parser.NextInt(0));
        position.Set("averageSpeed", parser.NextInt(0));
        position.Set("historyHighestSpeed", parser.NextInt(0));
        position.Set("historyHighestRpm", parser.NextInt(0));
        position.Set("totalHarshAccerleration", parser.NextInt(0));
        position.Set("totalHarshBrake", parser.NextInt(0));

        return position;
    }

    private Position DecodeDtc(Position position, string sentence)
    {
        GetLastLocation(position, null);
        position.Set(Position.KeyDtcs, sentence.Replace(',', ' '));
        return position;
    }

    private List<Position> DecodeRetransmission(ByteBuf buf, DeviceSession deviceSession)
    {
        var positions = new List<Position>();

        var count = buf.ReadUnsignedByte();
        for (var i = 0; i < count; i++)
        {
            buf.ReadUnsignedByte(); // alarm

            var endIndex = buf.IndexOf(buf.ReaderIndex, buf.WriterIndex, (byte)'\\');
            if (endIndex < 0)
            {
                endIndex = buf.WriterIndex - 4;
            }

            var sentence = buf.ReadString(endIndex - buf.ReaderIndex, Encoding.ASCII);

            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            var decoded = DecodeRegular(position, sentence);

            if (decoded != null)
            {
                positions.Add(decoded);
            }

            if (buf.ReadableBytes > 4)
            {
                buf.ReadUnsignedByte(); // delimiter
            }
        }

        return positions;
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = (IByteBuffer)message;
        var wrapped = new ByteBuf(buf);

        wrapped.SkipBytes(2); // header
        wrapped.ReadShort(); // length
        var id = wrapped.ReadSlice(7);
        var command = wrapped.ReadUnsignedShort();

        if (command == MsgLogin)
        {
            var response = Unpooled.WrappedBuffer([(byte)0x01]);
            SendResponse(channel, remoteAddress, id, MsgLoginResponse, response);
            return null;
        }
        if (command == MsgHeartbeat)
        {
            var response = Unpooled.WrappedBuffer([(byte)0x01]);
            SendResponse(channel, remoteAddress, id, MsgHeartbeat, response);
            return null;
        }
        if (command == MsgServer)
        {
            var response = Unpooled.CopiedBuffer(GetServer(channel, ':') ?? string.Empty, Encoding.ASCII);
            SendResponse(channel, remoteAddress, id, MsgServer, response);
            return null;
        }
        if (command == MsgUploadPhoto)
        {
            var imageIndex = wrapped.ReadByte();
            NewMediaBuffer();
            var response = Unpooled.CopiedBuffer([(byte)imageIndex]);
            SendResponse(channel, remoteAddress, id, MsgUploadPhotoResponse, response);
            return null;
        }
        if (command == MsgUploadComplete)
        {
            var imageIndex = wrapped.ReadByte();
            var response = Unpooled.CopiedBuffer([(byte)imageIndex, (byte)0, (byte)0]);
            SendResponse(channel, remoteAddress, id, MsgRetransmission, response);
            return null;
        }

        var deviceSession = Identify(new ByteBuf(id), channel, remoteAddress);
        if (deviceSession == null)
        {
            return null;
        }

        if (command == MsgDataPhoto)
        {
            wrapped.ReadByte(); // image index
            wrapped.ReadUnsignedShort(); // image footage
            wrapped.ReadUnsignedByte(); // total packets
            wrapped.ReadUnsignedByte(); // packet index

            var chunk = new byte[wrapped.ReadableBytes - 2 - 2];
            wrapped.ReadBytes(chunk);
            GetMediaBuffer()!.WriteBytes(chunk);

            return null;
        }
        if (command == MsgRetransmission)
        {
            return DecodeRetransmission(wrapped, deviceSession);
        }

        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        if (command == MsgAlarm)
        {
            var alarmCode = wrapped.ReadUnsignedByte();
            var model = GetDeviceModel(deviceSession);
            position.AddAlarm(DecodeAlarm(model, alarmCode));
            if (alarmCode is >= 0x02 and <= 0x05)
            {
                position.Set(Position.PrefixIn + alarmCode, 1);
            }
            else if (alarmCode is >= 0x32 and <= 0x35)
            {
                position.Set(Position.PrefixIn + (alarmCode - 0x30), 0);
            }
        }
        else if (command == MsgPositionLogged)
        {
            wrapped.SkipBytes(6);
        }
        else if (command == MsgRfid)
        {
            for (var i = 0; i < 15; i++)
            {
                var rfid = wrapped.ReadUnsignedInt();
                if (rfid != 0)
                {
                    var card = rfid.ToString("D10");
                    position.Set("card" + (i + 1), card);
                    position.Set(Position.KeyDriverUniqueId, card);
                }
            }
        }
        else if (command == MsgPositionImage)
        {
            wrapped.ReadByte(); // image index
            wrapped.ReadUnsignedByte(); // image upload type
            position.Set(Position.KeyImage, WriteMediaFile(deviceSession.UniqueId, "jpg"));
        }

        var sentence = wrapped.ToString(wrapped.ReaderIndex, wrapped.ReadableBytes - 4, Encoding.ASCII);

        return command switch
        {
            MsgPosition or MsgPositionLogged or MsgAlarm or MsgPositionImage => DecodeRegular(position, sentence),
            MsgRfid => DecodeRfid(position, sentence),
            MsgObdRt => DecodeObd(position, sentence),
            MsgObdRta => DecodeObdA(position, sentence),
            MsgDtc => DecodeDtc(position, sentence),
            _ => null,
        };
    }
}
