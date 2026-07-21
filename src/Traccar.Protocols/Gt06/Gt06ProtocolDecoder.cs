using System.Net;
using System.Text.RegularExpressions;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Gt06;

public sealed class Gt06ProtocolDecoder(ConnectionManager connectionManager, ILogger<Gt06ProtocolDecoder> logger)
    : BaseProtocolDecoder("gt06", connectionManager, logger)
{
    public const int MsgLogin = 0x01;
    public const int MsgGps = 0x10;
    public const int MsgGpsLbs6 = 0x11;
    public const int MsgGpsLbs1 = 0x12;
    public const int MsgGpsLbs2 = 0x22;
    public const int MsgGpsLbsDriver = 0x25; // XT40
    public const int MsgGpsLbs3 = 0x37;
    public const int MsgGpsLbs4 = 0x2D;
    public const int MsgStatus = 0x13;
    public const int MsgSatellite = 0x14;
    public const int MsgString = 0x15;
    public const int MsgGpsLbsStatus1 = 0x16;
    public const int MsgWifi = 0x17;
    public const int MsgGpsLbsRfid = 0x17;
    public const int MsgGpsLbsStatus2 = 0x26;
    public const int MsgGpsLbsStatus3 = 0x27;
    public const int MsgLbsMultiple1 = 0x28;
    public const int MsgLbsMultiple2 = 0x2E;
    public const int MsgLbsMultiple3 = 0x24;
    public const int MsgLbsWifi = 0x2C;
    public const int MsgLbsExtend = 0x18;
    public const int MsgLbsStatus = 0x19;
    public const int MsgGpsPhone = 0x1A;
    public const int MsgGpsLbsExtend = 0x1E; // JI09
    public const int MsgHeartbeat = 0x23; // GK310
    public const int MsgAddressRequest = 0x2A; // GK310
    public const int MsgAddressResponse = 0x97; // GK310
    public const int MsgGpsLbs5 = 0x31; // AZ735 & SL4X
    public const int MsgGpsLbsStatus4 = 0x32; // AZ735 & SL4X
    public const int MsgWifi5 = 0x33; // AZ735 & SL4X
    public const int MsgLbs3 = 0x34; // SL4X
    public const int MsgAz735Gps = 0x32; // AZ735 (extended)
    public const int MsgAz735Alarm = 0x33; // AZ735 (only extended)
    public const int MsgX1Gps = 0x34;
    public const int MsgX1PhotoInfo = 0x35;
    public const int MsgX1PhotoData = 0x36;
    public const int MsgStatus2 = 0x36; // Jimi IoT 4G
    public const int MsgAlarmModule = 0x39; // Jimi IoT 4G
    public const int MsgWifiAlarm = 0xA9; // Jimi IoT 4G
    public const int MsgAttendance = 0xB0; // Jimi IoT 4G
    public const int MsgBluetoothClock = 0xB2; // Jimi IoT 4G
    public const int MsgDeviceStatus = 0xF1; // Jimi IoT 4G
    public const int MsgWifi2 = 0x69;
    public const int MsgGpsModular = 0x70;
    public const int MsgWifi4 = 0xF3;
    public const int MsgCommand0 = 0x80;
    public const int MsgCommand1 = 0x81;
    public const int MsgCommand2 = 0x82;
    public const int MsgTimeRequest = 0x8A; // GK310
    public const int MsgInfo = 0x94;
    public const int MsgSerial = 0x9B;
    public const int MsgStringInfo = 0x21;
    public const int MsgGpsLbs7 = 0xA0; // GK310 & JM-VL03
    public const int MsgLbs2 = 0xA1; // GK310
    public const int MsgWifi3 = 0xA2; // GK310
    public const int MsgGpsLbsStatus5 = 0xA2; // LWxG
    public const int MsgFenceSingle = 0xA3; // GK310
    public const int MsgFenceMulti = 0xA4; // GK310 & JM-LL301
    public const int MsgLbsAlarm = 0xA5; // GK310 & JM-LL301
    public const int MsgLbsAddress = 0xA7; // GK310
    public const int MsgObd = 0x8C; // FM08ABC
    public const int MsgDtc = 0x65; // FM08ABC
    public const int MsgPid = 0x66; // FM08ABC
    public const int MsgBms = 0x40; // WD-209
    public const int MsgMultimedia = 0x41; // WD-209
    public const int MsgAlarm = 0x95; // JC100
    public const int MsgPeripheral = 0xF2; // VL842
    public const int MsgStatus3 = 0xA3; // GL21L
    public const int MsgGpsLbs8 = 0x38;
    public const int MsgIButton = 0x61;

    private static readonly HashSet<string> NtModels = ["NT11", "NT20", "NT26", "NT40", "NT46", "VL100", "XT40"];
    private static readonly HashSet<string> VlModels = ["VL103", "LL303", "VL512", "G18"];

    private enum Variant
    {
        Vxt01,
        WanwayS20,
        Sr411Mini,
        Gt06ECard,
        Benway,
        S5,
        Space10X,
        Standard,
        Obd6,
        Wetrust,
        Jc400,
        Sl4X,
        Seeworld,
        Rfid,
        Lw4G,
    }

    private Variant _variant;

    private static readonly Regex PatternFuel = new PatternBuilder()
        .Text("!AIOIL,")
        .Number("d+,") // device address
        .Number("d+.d+,") // output value
        .Number("(d+.d+),") // temperature
        .Expression("[^,]+,") // version
        .Number("dd") // back wave
        .Number("d") // software status code
        .Number("d,") // hardware status code
        .Number("(d+.d+),") // measured value
        .Expression("[01],") // movement status
        .Number("d+,") // excited wave times
        .Number("xx") // checksum
        .Compile();

    private static readonly Regex PatternLocation = new PatternBuilder()
        .Text("Current position!")
        .Number("Lat:([NS])(d+.d+),") // latitude
        .Number("Lon:([EW])(d+.d+),") // longitude
        .Text("Course:").Number("(d+.d+),") // course
        .Text("Speed:").Number("(d+.d+),") // speed
        .Text("DateTime:")
        .Number("(dddd)-(dd)-(dd) +") // date
        .Number("(dd):(dd):(dd)") // time
        .Compile();

    private bool IsSupported(int type, string? model) => HasGps(type) || HasLbs(type) || HasStatus(type, model);

    private bool HasGps(int type) => type switch
    {
        MsgGps or MsgGpsLbs1 or MsgGpsLbs2 or MsgGpsLbsDriver or MsgGpsLbs3 or MsgGpsLbs4 or MsgGpsLbs5
            or MsgGpsLbs6 or MsgGpsLbsStatus1 or MsgGpsLbsStatus2 or MsgGpsLbsStatus3 or MsgGpsLbsStatus4
            or MsgGpsLbsStatus5 or MsgGpsPhone or MsgGpsLbsExtend or MsgGpsLbs7 or MsgGpsLbsRfid
            or MsgFenceMulti or MsgAlarmModule => true,
        0xA3 => _variant != Variant.Seeworld, // MSG_FENCE_SINGLE / MSG_STATUS_3
        _ => false,
    };

    private bool HasLbs(int type) => type switch
    {
        MsgLbsStatus or MsgGpsLbs1 or MsgGpsLbs2 or MsgGpsLbsDriver or MsgGpsLbs3 or MsgGpsLbs4 or MsgGpsLbs5
            or MsgGpsLbs6 or MsgGpsLbsStatus1 or MsgGpsLbsStatus2 or MsgGpsLbsStatus3 or MsgGpsLbsStatus4
            or MsgGpsLbsStatus5 or MsgGpsLbs7 or MsgGpsLbsRfid or MsgFenceMulti or MsgAlarmModule
            or MsgLbsAlarm or MsgLbsAddress => true,
        0xA3 => _variant != Variant.Seeworld, // MSG_FENCE_SINGLE / MSG_STATUS_3
        _ => false,
    };

    private bool HasStatus(int type, string? model) => type switch
    {
        MsgStatus or MsgStatus2 or MsgLbsStatus or MsgGpsLbsStatus1 or MsgGpsLbsStatus2 or MsgGpsLbsStatus3
            or MsgGpsLbsStatus4 or MsgGpsLbsStatus5 or MsgFenceMulti or MsgAlarmModule or MsgLbsAlarm => true,
        MsgGpsLbs2 or MsgGpsLbsDriver => model != null && NtModels.Contains(model.ToUpperInvariant()),
        0xA3 => _variant == Variant.Seeworld, // MSG_FENCE_SINGLE / MSG_STATUS_3
        _ => false,
    };

    private static void SendResponse(IChannel channel, bool extended, int type, int index, IByteBuffer? content)
    {
        var response = Unpooled.Buffer();
        var length = 5 + (content?.ReadableBytes ?? 0);
        if (extended)
        {
            response.WriteShort(0x7979);
            response.WriteShort(length);
        }
        else
        {
            response.WriteShort(0x7878);
            response.WriteByte(length);
        }
        response.WriteByte(type);
        if (content != null)
        {
            response.WriteBytes(content);
        }
        response.WriteShort(index);
        var crcBytes = new byte[response.WriterIndex - 2];
        response.GetBytes(2, crcBytes);
        var crc = Checksum.Crc16(Checksum.Crc16X25, crcBytes);
        response.WriteShort(crc);
        response.WriteByte('\r');
        response.WriteByte('\n');
        channel.WriteAndFlushAsync(response);
    }

    private void SendPhotoRequest(IChannel channel, int pictureId)
    {
        var photo = GetMediaBuffer()!;
        var content = Unpooled.Buffer();
        content.WriteInt(pictureId);
        content.WriteInt(photo.WriterIndex);
        content.WriteShort(Math.Min(photo.WritableBytes, 1024));
        SendResponse(channel, false, MsgX1PhotoData, 0, content);
    }

    public static bool DecodeGps(Position position, ByteBuf buf, bool hasLength, TimeZoneInfo? timeZone)
        => DecodeGps(position, buf, hasLength, true, true, false, false, timeZone);

    public static bool DecodeGps(
        Position position, ByteBuf buf, bool hasLength, bool hasSatellites,
        bool hasSpeed, bool longSpeed, bool swapFlags, TimeZoneInfo? timeZone)
    {
        var dateBuilder = new DateBuilder(timeZone)
            .SetDate(buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte())
            .SetTime(buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte());
        position.SetTime(dateBuilder.GetDate());

        if (hasLength && buf.ReadUnsignedByte() == 0)
        {
            return false;
        }

        if (hasSatellites)
        {
            position.Set(Position.KeySatellites, BitUtil.To(buf.ReadUnsignedByte(), 4));
        }

        var latitude = buf.ReadUnsignedInt() / 60.0 / 30000.0;
        var longitude = buf.ReadUnsignedInt() / 60.0 / 30000.0;

        var flags = 0;
        if (swapFlags)
        {
            flags = buf.ReadUnsignedShort();
        }
        if (hasSpeed)
        {
            position.Speed = UnitsConverter.KnotsFromKph(longSpeed ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte());
        }
        if (!swapFlags)
        {
            flags = buf.ReadUnsignedShort();
        }

        position.Course = BitUtil.To(flags, 10);
        position.Valid = BitUtil.Check(flags, 12);

        if (!BitUtil.Check(flags, 10))
        {
            latitude = -latitude;
        }
        if (BitUtil.Check(flags, 11))
        {
            longitude = -longitude;
        }

        position.Latitude = latitude;
        position.Longitude = longitude;

        if (BitUtil.Check(flags, 14))
        {
            position.Set(Position.KeyIgnition, BitUtil.Check(flags, 15));
        }

        return true;
    }

    private bool DecodeLbs(Position position, ByteBuf buf, int type, bool hasLength)
    {
        var length = 0;
        if (hasLength)
        {
            length = buf.ReadUnsignedByte();
            if (length == 0)
            {
                var zeroedData = true;
                for (var i = buf.ReaderIndex + 9; i < buf.ReaderIndex + 45 && i < buf.WriterIndex; i++)
                {
                    if (buf.GetByte(i) != 0)
                    {
                        zeroedData = false;
                        break;
                    }
                }
                if (zeroedData)
                {
                    buf.SkipBytes(Math.Min(buf.ReadableBytes, 45));
                }
                return false;
            }
        }

        var cellType = type == MsgGpsLbs8 || type == MsgAlarmModule ? buf.ReadUnsignedByte() : 0;
        var mcc = buf.ReadUnsignedShort();
        int mnc;
        if (BitUtil.Check(mcc, 15) || type == MsgGpsLbs6 || _variant == Variant.Sl4X)
        {
            mnc = buf.ReadUnsignedShort();
        }
        else
        {
            mnc = buf.ReadUnsignedByte();
        }
        long lac;
        if (cellType >= 3 || type == MsgLbsAlarm || type == MsgGpsLbs7 || type == MsgGpsLbsStatus5)
        {
            lac = buf.ReadInt();
        }
        else
        {
            lac = buf.ReadUnsignedShort();
        }
        long cid;
        if (cellType >= 3 || type == MsgLbsAlarm || type == MsgGpsLbs7 || _variant == Variant.Sl4X
            || type == MsgGpsLbsStatus5)
        {
            cid = buf.ReadLong();
        }
        else if (type == MsgGpsLbs6 || type == MsgIButton || type == MsgGpsLbsDriver || _variant == Variant.Seeworld)
        {
            cid = buf.ReadUnsignedInt();
        }
        else
        {
            cid = buf.ReadUnsignedMedium();
        }
        if (cellType >= 3)
        {
            buf.ReadUnsignedShort(); // rssi
        }
        else if (type == MsgGpsLbs8 || type == MsgAlarmModule)
        {
            buf.ReadUnsignedByte(); // rssi
        }

        var network = new Network();
        network.AddCellTower(CellTower.From((int)BitUtil.To(mcc, 15), mnc, (int)lac, cid));
        position.Network = network;

        if (length > 9)
        {
            buf.SkipBytes(length - 9);
        }

        return true;
    }

    private void DecodeStatus(Position position, ByteBuf buf)
    {
        var status = buf.ReadUnsignedByte();

        position.Set(Position.KeyStatus, status);
        position.Set(Position.KeyIgnition, BitUtil.Check(status, 1));
        position.Set(Position.KeyCharge, BitUtil.Check(status, 2));
        position.Set(Position.KeyBlocked, BitUtil.Check(status, 7));

        switch (BitUtil.Between(status, 3, 6))
        {
            case 1: position.AddAlarm(Position.AlarmVibration); break;
            case 2: position.AddAlarm(Position.AlarmPowerCut); break;
            case 3: position.AddAlarm(Position.AlarmLowBattery); break;
            case 4: position.AddAlarm(Position.AlarmSos); break;
            case 6: position.AddAlarm(Position.AlarmGeofence); break;
            case 7:
                position.AddAlarm(_variant == Variant.Vxt01 ? Position.AlarmOverspeed : Position.AlarmRemoving);
                break;
        }
    }

    private static string? DecodeAlarm(int value, bool modelLw, bool modelSw, bool modelVl) => value switch
    {
        0x01 => Position.AlarmSos,
        0x02 => Position.AlarmPowerCut,
        0x03 => Position.AlarmVibration,
        0x04 => Position.AlarmGeofenceEnter,
        0x05 => Position.AlarmGeofenceExit,
        0x06 => Position.AlarmOverspeed,
        0x09 => modelVl ? Position.AlarmTow : Position.AlarmVibration,
        0x0E or 0x0F => Position.AlarmLowBattery,
        0x11 => Position.AlarmPowerOff,
        0x0C or 0x13 or 0x25 or 0x32 => Position.AlarmTampering,
        0x14 => Position.AlarmDoor,
        0x18 => modelLw ? Position.AlarmAccident : Position.AlarmRemoving,
        0x19 => modelLw ? Position.AlarmAcceleration : Position.AlarmLowBattery,
        0x1A or 0x27 => Position.AlarmBraking,
        0x1B or 0x2A or 0x2B or 0x2E => Position.AlarmCornering,
        0x23 => Position.AlarmFallDown,
        0x26 => Position.AlarmAcceleration,
        0x28 => modelSw ? Position.AlarmCornering : modelVl ? Position.AlarmPowerOff : Position.AlarmBraking,
        0x29 => modelSw ? Position.AlarmAccident : Position.AlarmAcceleration,
        0x2C => Position.AlarmAccident,
        0x30 => modelVl ? Position.AlarmBraking : Position.AlarmJamming,
        0x33 => Position.AlarmLock,
        0x34 => Position.AlarmUnlock,
        0x53 => Position.AlarmFuelLeak,
        0x5B or 0x5C => Position.AlarmTemperature,
        0xC9 => Position.AlarmIdle,
        0x0107 or 0x010B => Position.AlarmJamming,
        _ => null,
    };

    private static DateTime DecodeDate(ByteBuf buf, DeviceSession deviceSession)
    {
        var dateBuilder = new DateBuilder(deviceSession.Get<TimeZoneInfo>(DeviceSession.KeyTimezone))
            .SetDate(buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte())
            .SetTime(buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte());
        return dateBuilder.GetDate();
    }

    private void DecodeVariant(ByteBuf buf)
    {
        var header = buf.GetUnsignedShort(buf.ReaderIndex);
        int length;
        int type;
        if (header == 0x7878)
        {
            length = buf.GetUnsignedByte(buf.ReaderIndex + 2);
            type = buf.GetUnsignedByte(buf.ReaderIndex + 2 + 1);
        }
        else
        {
            length = buf.GetUnsignedShort(buf.ReaderIndex + 2);
            type = buf.GetUnsignedByte(buf.ReaderIndex + 2 + 2);
        }

        if (header == 0x7878 && type == MsgGpsLbs1 && length == 0x24)
        {
            _variant = Variant.Vxt01;
        }
        else if (header == 0x7878 && type == MsgGpsLbsStatus1 && length == 0x24)
        {
            _variant = Variant.Vxt01;
        }
        else if (header == 0x7878 && type == MsgLbsMultiple3 && length == 0x31)
        {
            _variant = Variant.WanwayS20;
        }
        else if (header == 0x7878 && type == MsgLbsMultiple3 && length == 0x2e)
        {
            _variant = Variant.Sr411Mini;
        }
        else if (header == 0x7878 && type == MsgGpsLbs1 && length >= 0x71)
        {
            _variant = Variant.Gt06ECard;
        }
        else if (header == 0x7878 && type == MsgGpsLbs1 && length == 0x21)
        {
            _variant = Variant.Benway;
        }
        else if (header == 0x7878 && type == MsgGpsLbs1 && length == 0x2b)
        {
            _variant = Variant.S5;
        }
        else if (header == 0x7878 && type == MsgLbsStatus && length >= 0x17)
        {
            _variant = Variant.Space10X;
        }
        else if (header == 0x7878 && type == MsgStatus && length == 0x13)
        {
            _variant = Variant.Obd6;
        }
        else if (header == 0x7878 && type == MsgGpsLbs1 && length == 0x29)
        {
            _variant = Variant.Wetrust;
        }
        else if (header == 0x7878 && type == MsgAlarm && buf.GetUnsignedShort(buf.ReaderIndex + 4) == 0xffff)
        {
            _variant = Variant.Jc400;
        }
        else if (header == 0x7878 && type == MsgLbs3 && length == 0x37)
        {
            _variant = Variant.Sl4X;
        }
        else if (header == 0x7878 && type == MsgGpsLbs5 && length == 0x2a)
        {
            _variant = Variant.Sl4X;
        }
        else if (header == 0x7878 && type == MsgGpsLbsStatus4 && length == 0x27)
        {
            _variant = Variant.Sl4X;
        }
        else if (header == 0x7878 && type == MsgGpsLbsStatus4 && length == 0x29)
        {
            _variant = Variant.Sl4X;
        }
        else if (header == 0x7878 && type == MsgGpsLbs2 && length == 0x2f)
        {
            _variant = Variant.Seeworld;
        }
        else if (header == 0x7878 && type == MsgGpsLbsStatus1 && length == 0x26)
        {
            _variant = Variant.Seeworld;
        }
        else if (header == 0x7878 && type == MsgStatus3 && length == 0x0c)
        {
            _variant = Variant.Seeworld;
        }
        else if (header == 0x7878 && type == MsgGpsLbsRfid && length == 0x28)
        {
            _variant = Variant.Rfid;
        }
        else if (header == 0x7878 && type == MsgGpsLbsStatus5 && length == 0x40)
        {
            _variant = Variant.Lw4G;
        }
        else
        {
            _variant = Variant.Standard;
        }
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = new ByteBuf((IByteBuffer)message);

        DecodeVariant(buf);

        var extended = buf.ReadShort() != 0x7878;

        var length = extended ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte();
        var dataLength = length - 5;
        var type = buf.ReadUnsignedByte();

        var position = new Position(ProtocolName);
        DeviceSession? deviceSession = null;
        if (type != MsgLogin)
        {
            deviceSession = GetDeviceSession(channel, remoteAddress);
            if (deviceSession == null)
            {
                return null;
            }
            position.DeviceId = deviceSession.DeviceId;
        }

        var model = deviceSession != null ? GetDeviceModel(deviceSession) : null;
        model = model?.ToUpperInvariant();
        var modelLw = model != null && model.StartsWith("LW", StringComparison.Ordinal);
        var modelSw = model == "SEEWORLD";
        var modelNt = model != null && NtModels.Contains(model);
        var modelVl = model != null && VlModels.Contains(model);

        if (type == MsgLogin)
        {
            var imei = ByteBufferUtil.HexDump(buf.ReadSlice(8))[1..];
            buf.ReadUnsignedShort(); // type

            deviceSession = GetDeviceSession(channel, remoteAddress, imei);
            if (deviceSession != null)
            {
                // Extension bits, when present, carry the device's own local-time offset; unlike
                // Java, this port has no per-device config override to fall back to first.
                TimeZoneInfo? timeZone = null;
                if (dataLength > 10)
                {
                    var extensionBits = buf.ReadUnsignedShort();
                    var hours = (extensionBits >> 4) / 100;
                    var minutes = (extensionBits >> 4) % 100;
                    var offset = (hours * 60 + minutes) * 60;
                    if ((extensionBits & 0x8) != 0)
                    {
                        offset = -offset;
                    }
                    timeZone = TimeZoneInfo.CreateCustomTimeZone(
                        "gt06-login", TimeSpan.FromSeconds(offset), "gt06-login", "gt06-login");
                }
                deviceSession.Set(DeviceSession.KeyTimezone, timeZone);

                SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);
            }

            return null;
        }

        if (type == MsgHeartbeat)
        {
            GetLastLocation(position, null);

            var status = buf.ReadUnsignedByte();
            position.Set(Position.KeyArmed, BitUtil.Check(status, 0));
            position.Set(Position.KeyIgnition, BitUtil.Check(status, 1));
            position.Set(Position.KeyCharge, BitUtil.Check(status, 2));

            if (buf.ReadableBytes >= 2 + 6)
            {
                position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0);
            }
            if (buf.ReadableBytes >= 1 + 6)
            {
                position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
            }

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgAddressRequest)
        {
            const string response = "NA&&NA&&0##";
            var content = Unpooled.Buffer();
            content.WriteByte(response.Length);
            content.WriteInt(0);
            content.WriteBytes(System.Text.Encoding.ASCII.GetBytes(response));
            SendResponse(channel, true, MsgAddressResponse, 0, content);

            return null;
        }

        if (type == MsgTimeRequest)
        {
            var now = DateTime.UtcNow;
            var content = Unpooled.Buffer();
            content.WriteByte(now.Year - 2000);
            content.WriteByte(now.Month);
            content.WriteByte(now.Day);
            content.WriteByte(now.Hour);
            content.WriteByte(now.Minute);
            content.WriteByte(now.Second);
            SendResponse(channel, false, MsgTimeRequest, 0, content);

            return null;
        }

        if (type == MsgX1Gps && _variant != Variant.Sl4X)
        {
            buf.ReadUnsignedInt(); // data and alarm

            DecodeGps(position, buf, false, deviceSession!.Get<TimeZoneInfo>(DeviceSession.KeyTimezone));

            buf.ReadUnsignedShort(); // terminal info

            position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());

            var network1 = new Network();
            network1.AddCellTower(CellTower.From(
                buf.ReadUnsignedShort(), buf.ReadUnsignedByte(), buf.ReadUnsignedShort(), buf.ReadUnsignedInt()));
            position.Network = network1;

            var driverId = buf.ReadUnsignedInt();
            if (driverId > 0)
            {
                position.Set(Position.KeyDriverUniqueId, driverId.ToString());
            }

            position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0);
            position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);

            var portInfo = buf.ReadUnsignedInt();

            position.Set(Position.KeyInput, buf.ReadUnsignedByte());
            position.Set(Position.KeyOutput, buf.ReadUnsignedByte());

            for (var i = 1; i <= BitUtil.Between(portInfo, 20, 24); i++)
            {
                position.Set(Position.PrefixAdc + i, buf.ReadUnsignedShort() / 100.0);
            }

            return position;
        }

        if (type == MsgX1PhotoInfo)
        {
            buf.SkipBytes(6); // time
            buf.ReadUnsignedByte(); // fix status
            buf.ReadUnsignedInt(); // latitude
            buf.ReadUnsignedInt(); // longitude
            buf.ReadUnsignedByte(); // camera id
            buf.ReadUnsignedByte(); // photo source
            buf.ReadUnsignedByte(); // picture format

            NewMediaBuffer(buf.ReadInt());
            var pictureId = buf.ReadInt();
            SendPhotoRequest(channel, pictureId);

            return null;
        }

        if ((type == MsgWifi && _variant != Variant.Rfid) || type == MsgWifi2 || type == MsgWifi4)
        {
            var time = buf.ReadSlice(6);
            var timeBuf = new ByteBuf(time);
            var dateBuilder = new DateBuilder()
                .SetYear(BcdUtil.ReadInteger(timeBuf, 2))
                .SetMonth(BcdUtil.ReadInteger(timeBuf, 2))
                .SetDay(BcdUtil.ReadInteger(timeBuf, 2))
                .SetHour(BcdUtil.ReadInteger(timeBuf, 2))
                .SetMinute(BcdUtil.ReadInteger(timeBuf, 2))
                .SetSecond(BcdUtil.ReadInteger(timeBuf, 2));
            GetLastLocation(position, dateBuilder.GetDate());

            var network = new Network();

            int wifiCount;
            if (type == MsgWifi4)
            {
                wifiCount = buf.ReadUnsignedByte();
            }
            else
            {
                wifiCount = buf.GetUnsignedByte(2);
            }

            for (var i = 0; i < wifiCount; i++)
            {
                if (type == MsgWifi4)
                {
                    buf.SkipBytes(2);
                }
                var mac = string.Format(
                    "{0:x2}:{1:x2}:{2:x2}:{3:x2}:{4:x2}:{5:x2}",
                    buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte(),
                    buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte());
                var wifiAccessPoint = new WifiAccessPoint { MacAddress = mac };
                if (type != MsgWifi4)
                {
                    wifiAccessPoint.SignalStrength = buf.ReadUnsignedByte();
                }
                network.AddWifiAccessPoint(wifiAccessPoint);
            }

            if (type != MsgWifi4)
            {
                var cellCount = buf.ReadUnsignedByte();
                var mcc = buf.ReadUnsignedShort();
                var mnc = buf.ReadUnsignedByte();
                for (var i = 0; i < cellCount; i++)
                {
                    network.AddCellTower(CellTower.From(
                        mcc, mnc, buf.ReadUnsignedShort(), buf.ReadUnsignedShort(), buf.ReadUnsignedByte()));
                }

                var response = Unpooled.Buffer();
                response.WriteShort(0x7878);
                response.WriteByte(0);
                response.WriteByte(type);
                time.SetReaderIndex(0);
                response.WriteBytes(time);
                response.WriteByte('\r');
                response.WriteByte('\n');
                channel.WriteAndFlushAsync(response);
            }

            position.Network = network;

            return position;
        }

        if (type == MsgInfo && !extended)
        {
            GetLastLocation(position, null);

            position.Set(Position.KeyPower, buf.ReadShort() / 100.0);

            return position;
        }

        if (type == MsgLbsMultiple3 && _variant == Variant.Sr411Mini)
        {
            DecodeGps(position, buf, false, deviceSession!.Get<TimeZoneInfo>(DeviceSession.KeyTimezone));

            DecodeLbs(position, buf, type, false);

            position.Set(Position.KeyIgnition, buf.ReadUnsignedByte() > 0);
            position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
            position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0);

            return position;
        }

        if (type == MsgLbsMultiple1 || type == MsgLbsMultiple2 || type == MsgLbsMultiple3
            || type == MsgLbsExtend || type == MsgLbsWifi || type == MsgLbs2 || type == MsgLbs3
            || (type == MsgWifi3 && _variant != Variant.Lw4G) || (type == MsgWifi5 && !extended)
            || type == MsgWifiAlarm)
        {
            GetLastLocation(position, DecodeDate(buf, deviceSession!));

            if (_variant == Variant.WanwayS20 || _variant == Variant.Sl4X)
            {
                buf.ReadUnsignedByte(); // ta
            }

            var mcc = buf.ReadUnsignedShort();
            var mnc = BitUtil.Check(mcc, 15) || _variant == Variant.Sl4X ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte();
            var network = new Network();

            var cell4G = type == MsgWifiAlarm && buf.ReadUnsignedByte() > 0;

            var cellCount = _variant == Variant.WanwayS20 || type == MsgWifiAlarm
                ? buf.ReadUnsignedByte() : type == MsgWifi5 ? 6 : 7;
            for (var i = 0; i < cellCount; i++)
            {
                long lac;
                long cid;
                if (type == MsgLbs2 || type == MsgWifi3 || cell4G)
                {
                    lac = buf.ReadInt();
                    cid = buf.ReadLong();
                }
                else if (type == MsgWifi5 || type == MsgLbs3)
                {
                    lac = buf.ReadUnsignedShort();
                    cid = buf.ReadUnsignedInt();
                }
                else
                {
                    lac = buf.ReadUnsignedShort();
                    cid = buf.ReadUnsignedMedium();
                }
                var rssi = -buf.ReadUnsignedByte();
                if (lac > 0)
                {
                    network.AddCellTower(CellTower.From((int)BitUtil.To(mcc, 15), mnc, (int)lac, cid, rssi));
                }
            }

            if (_variant != Variant.WanwayS20 && _variant != Variant.Sl4X)
            {
                buf.ReadUnsignedByte(); // ta
            }

            if (type != MsgLbsMultiple1 && type != MsgLbsMultiple2 && type != MsgLbsMultiple3
                && type != MsgLbs2 && type != MsgLbs3)
            {
                var wifiCount = buf.ReadUnsignedByte();
                for (var i = 0; i < wifiCount; i++)
                {
                    var mac = ByteBufferUtil.HexDump(buf.ReadSlice(6));
                    mac = Regex.Replace(mac, "(..)", "$1:");
                    network.AddWifiAccessPoint(WifiAccessPoint.From(mac[..^1], buf.ReadUnsignedByte()));
                }
            }

            position.Network = network;

            if (type == MsgWifiAlarm)
            {
                var alarm = buf.ReadUnsignedByte();
                var language = buf.ReadUnsignedByte();
                position.AddAlarm(DecodeAlarm((language >> 4) << 8 | alarm, modelLw, modelSw, modelVl));
            }

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgBluetoothClock)
        {
            GetLastLocation(position, DecodeDate(buf, deviceSession!));

            position.Set(Position.KeyRssi, buf.ReadByte());
            position.Set("tagMac", ByteBufferUtil.HexDump(buf.ReadSlice(6)));
            position.Set("tagUuid", ByteBufferUtil.HexDump(buf.ReadSlice(16)));
            position.Set("tagMajor", buf.ReadUnsignedShort());
            position.Set("tagMinor", buf.ReadUnsignedShort());
            position.Set("tagBattery", buf.ReadUnsignedShort() / 100.0);
            position.Set("clock", BitUtil.Between(buf.ReadUnsignedByte(), 2, 6));

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgString)
        {
            GetLastLocation(position, null);

            var commandLength = buf.ReadUnsignedByte();

            if (commandLength > 0)
            {
                buf.ReadUnsignedInt(); // server flag (reserved)
                var data = buf.ReadString(commandLength - 4, System.Text.Encoding.ASCII);
                if (data.StartsWith("<ICCID:", StringComparison.Ordinal))
                {
                    position.Set(Position.KeyIccid, data.Substring(7, 20));
                }
                else
                {
                    position.Set(Position.KeyResult, data);
                }
            }

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgBms)
        {
            buf.SkipBytes(8); // serial number

            GetLastLocation(position, DateTimeOffset.FromUnixTimeSeconds(buf.ReadUnsignedInt()).UtcDateTime);

            position.Set("relativeCapacity", buf.ReadUnsignedByte());
            position.Set("remainingCapacity", buf.ReadUnsignedShort());
            position.Set("absoluteCapacity", buf.ReadUnsignedByte());
            position.Set("fullCapacity", buf.ReadUnsignedShort());
            position.Set("batteryHealth", buf.ReadUnsignedByte());
            position.Set("batteryTemp", buf.ReadUnsignedShort() / 10.0 - 273.1);
            position.Set("current", buf.ReadUnsignedShort());
            position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 1000.0);
            position.Set("cycleIndex", buf.ReadUnsignedShort());
            for (var i = 1; i <= 14; i++)
            {
                position.Set("batteryCell" + i, buf.ReadUnsignedShort() / 1000.0);
            }
            position.Set("currentChargeInterval", buf.ReadUnsignedShort());
            position.Set("maxChargeInterval", buf.ReadUnsignedShort());
            position.Set("barcode", buf.ReadString(16, System.Text.Encoding.ASCII).Trim());
            position.Set("batteryVersion", buf.ReadUnsignedShort());
            position.Set("manufacturer", buf.ReadString(16, System.Text.Encoding.ASCII).Trim());
            position.Set("batteryStatus", buf.ReadUnsignedInt());

            position.Set("controllerStatus", buf.ReadUnsignedInt());
            position.Set("controllerFault", buf.ReadUnsignedInt());

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgStatus && buf.ReadableBytes == 22)
        {
            GetLastLocation(position, null);

            buf.ReadUnsignedByte(); // information content
            buf.ReadUnsignedShort(); // satellites
            buf.ReadUnsignedByte(); // alarm
            buf.ReadUnsignedByte(); // language

            position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());

            buf.ReadUnsignedByte(); // working mode
            buf.ReadUnsignedShort(); // working voltage
            buf.ReadUnsignedByte(); // reserved
            buf.ReadUnsignedShort(); // working times
            buf.ReadUnsignedShort(); // working time

            var value = buf.ReadUnsignedShort();
            var temperature = BitUtil.To(value, 15) / 10.0;
            position.Set(Position.PrefixTemp + 1, BitUtil.Check(value, 15) ? temperature : -temperature);

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgIButton)
        {
            buf.SkipBytes(8); // imei

            DecodeGps(position, buf, false, deviceSession!.Get<TimeZoneInfo>(DeviceSession.KeyTimezone));

            DecodeLbs(position, buf, type, false);

            var driverUniqueId = (buf.ReadUnsignedInt() << 16) | (uint)buf.ReadUnsignedShort();
            position.Set(Position.KeyDriverUniqueId, driverUniqueId.ToString());

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (IsSupported(type, model) && !(extended && (type == MsgGpsLbsStatus4 || type == MsgStatus2)))
        {
            if (type == MsgLbsStatus && _variant == Variant.Space10X)
            {
                return null; // multi-lbs message
            }

            position.Set(Position.KeyType, type);

            if (type == MsgGpsLbs1 && model == "QH302R")
            {
                buf.SkipBytes(8); // imei
            }
            if ((type == MsgGpsLbs2 || type == MsgGpsLbsDriver) && modelNt)
            {
                buf.ReadUnsignedByte(); // location source type
                buf.SkipBytes(8); // imei
                position.DeviceTime = DecodeDate(buf, deviceSession!);
            }

            if (HasGps(type))
            {
                DecodeGps(position, buf, false, deviceSession!.Get<TimeZoneInfo>(DeviceSession.KeyTimezone));
            }
            else
            {
                GetLastLocation(position, null);
            }

            if (HasLbs(type) && buf.ReadableBytes > 6)
            {
                var hasLength = HasStatus(type, model)
                    && type != MsgLbsStatus
                    && type != MsgLbsAlarm
                    && type != MsgAlarmModule
                    && (type != MsgGpsLbsStatus1 || _variant != Variant.Vxt01)
                    && type != MsgGpsLbsStatus5;
                DecodeLbs(position, buf, type, hasLength);
            }

            if (HasStatus(type, model))
            {
                if (type == MsgGpsLbsStatus5)
                {
                    buf.ReadUnsignedByte(); // network indicator
                }
                DecodeStatus(position, buf);
                if (_variant == Variant.Obd6)
                {
                    var signal = buf.ReadUnsignedShort();
                    var satellites = BitUtil.Between(signal, 10, 15) + BitUtil.Between(signal, 5, 10);
                    position.Set(Position.KeySatellites, satellites);
                    position.Set(Position.KeyRssi, BitUtil.To(signal, 5));
                    position.AddAlarm(DecodeAlarm(buf.ReadUnsignedByte(), modelLw, modelSw, modelVl));
                    buf.ReadUnsignedByte(); // language
                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                    var mode = buf.ReadUnsignedByte();
                    position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                    buf.ReadUnsignedByte(); // reserved
                    buf.ReadUnsignedShort(); // working time
                    if (mode == 4)
                    {
                        position.Set(Position.PrefixTemp + 1, buf.ReadShort() / 10.0);
                    }
                }
                else
                {
                    if (type == MsgGpsLbsStatus5 || (modelNt && (type == MsgGpsLbs2 || type == MsgGpsLbsDriver)))
                    {
                        position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                    }
                    if (type == MsgStatus && model == "R11")
                    {
                        position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                    }
                    else
                    {
                        var battery = buf.ReadUnsignedByte();
                        if (modelNt && (type == MsgGpsLbs2 || type == MsgGpsLbsDriver))
                        {
                            position.Set(Position.KeyBattery, battery / 10.0);
                        }
                        else if (battery <= 6)
                        {
                            position.Set(Position.KeyBatteryLevel, battery * 100 / 6);
                        }
                        else if (battery <= 100)
                        {
                            position.Set(Position.KeyBatteryLevel, battery);
                        }
                    }
                    position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                    if (type == MsgStatus && modelLw)
                    {
                        position.Set(Position.KeyPower, BitUtil.To(buf.ReadUnsignedShort(), 12) / 10.0);
                    }
                    else
                    {
                        var extension = buf.ReadUnsignedByte();
                        if (type == MsgGpsLbsStatus3 || type == MsgFenceMulti || type == MsgAlarmModule)
                        {
                            extension += (buf.GetUnsignedByte(buf.ReaderIndex) >> 4) << 8;
                        }
                        if (type == MsgStatus && modelSw)
                        {
                            position.Set(Position.KeyPower, (double)extension);
                        }
                        else if (_variant != Variant.Vxt01)
                        {
                            position.AddAlarm(DecodeAlarm(extension, modelLw, modelSw, modelVl));
                        }
                    }
                }
            }

            if (type == MsgStatus2 || type == MsgGpsLbsStatus5 || type == MsgGpsLbsStatus3
                || type == MsgFenceMulti || type == MsgAlarmModule
                || (modelNt && (type == MsgGpsLbs2 || type == MsgGpsLbsDriver)))
            {
                buf.ReadUnsignedByte(); // language
            }

            if (type == MsgStatus2 || type == MsgAlarmModule)
            {
                while (buf.ReadableBytes > 6)
                {
                    var moduleType = buf.ReadUnsignedShort();
                    var moduleLength = buf.ReadUnsignedByte();
                    switch (moduleType)
                    {
                        case 0x0018: position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0); break;
                        case 0x0032: position.Set("startupStatus", buf.ReadUnsignedByte()); break;
                        case 0x006A: position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte()); break;
                        default: buf.SkipBytes(moduleLength); break;
                    }
                }
            }

            if (type == MsgGpsLbsStatus5)
            {
                position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
                position.Set(Position.KeyHours, buf.ReadUnsignedInt() * 1000);
                buf.SkipBytes(8); // terminal id
                position.Set(Position.KeyInput, buf.ReadUnsignedShort());
            }

            if (type == MsgGpsLbs1)
            {
                if (_variant == Variant.Gt06ECard)
                {
                    position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
                    var data = buf.ReadString(buf.ReadUnsignedByte(), System.Text.Encoding.ASCII);
                    buf.ReadUnsignedByte(); // alarm
                    buf.ReadUnsignedByte(); // swiped
                    position.Set(Position.KeyCard, data.Trim());
                }
                else if (_variant == Variant.Benway)
                {
                    var mask = buf.ReadUnsignedShort();
                    position.Set(Position.KeyIgnition, BitUtil.Check(mask, 8 + 7));
                    position.Set(Position.PrefixIn + 2, BitUtil.Check(mask, 8 + 6));
                    if (BitUtil.Check(mask, 8 + 4))
                    {
                        var value = BitUtil.To(mask, 8 + 1);
                        if (BitUtil.Check(mask, 8 + 1))
                        {
                            value = -value;
                        }
                        position.Set(Position.PrefixTemp + 1, value);
                    }
                    else
                    {
                        var value = BitUtil.To(mask, 8 + 2);
                        if (BitUtil.Check(mask, 8 + 5))
                        {
                            position.Set(Position.PrefixAdc + 1, value);
                        }
                        else
                        {
                            position.Set(Position.PrefixAdc + 1, value / 10.0);
                        }
                    }
                }
                else if (_variant == Variant.Vxt01)
                {
                    DecodeStatus(position, buf);
                    position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                    position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                    buf.ReadUnsignedByte(); // alarm extension
                }
                else if (_variant == Variant.S5)
                {
                    DecodeStatus(position, buf);
                    position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                    position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                    position.AddAlarm(DecodeAlarm(buf.ReadUnsignedByte(), modelLw, modelSw, modelVl));
                    position.Set("oil", buf.ReadUnsignedShort());
                    var temperature = buf.ReadUnsignedByte();
                    if (BitUtil.Check(temperature, 7))
                    {
                        temperature = -BitUtil.To(temperature, 7);
                    }
                    position.Set(Position.PrefixTemp + 1, temperature);
                    position.Set(Position.KeyOdometer, buf.ReadUnsignedInt() * 10);
                }
                else if (_variant == Variant.Wetrust)
                {
                    position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
                    position.Set(Position.KeyCard, buf.ReadString(buf.ReadUnsignedByte(), System.Text.Encoding.ASCII));
                    position.AddAlarm(buf.ReadUnsignedByte() > 0 ? Position.AlarmGeneral : null);
                    position.Set("cardStatus", buf.ReadUnsignedByte());
                    position.Set(Position.KeyDrivingTime, buf.ReadUnsignedShort());
                }
            }

            if (type == MsgGpsLbs2 && _variant == Variant.Seeworld)
            {
                position.Set(Position.KeyIgnition, buf.ReadUnsignedByte() > 0);
                buf.ReadUnsignedByte(); // reporting mode
                buf.ReadUnsignedByte(); // supplementary transmission
                position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
                buf.ReadUnsignedInt(); // travel time
                var temperature = buf.ReadUnsignedShort();
                if (BitUtil.Check(temperature, 15))
                {
                    temperature = -BitUtil.To(temperature, 15);
                }
                position.Set(Position.PrefixTemp + 1, temperature / 100.0);
                position.Set(Position.KeyHumidity, buf.ReadUnsignedShort() / 100.0);
            }

            if (type == MsgGpsLbsStatus4 && _variant == Variant.Sl4X)
            {
                position.Altitude = buf.ReadShort();
            }

            if ((type == MsgGpsLbs2 || type == MsgGpsLbs3 || type == MsgGpsLbs4 || type == MsgGpsLbs5)
                && buf.ReadableBytes >= 3 + 6 && !modelNt)
            {
                position.Set(Position.KeyIgnition, buf.ReadUnsignedByte() > 0);
                position.Set(Position.KeyEvent, buf.ReadUnsignedByte()); // reason
                if (buf.ReadUnsignedByte() > 0)
                {
                    position.Set(Position.KeyArchive, true);
                }
                if (_variant == Variant.Sl4X)
                {
                    if (buf.ReadableBytes > 2 + 6)
                    {
                        position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
                    }
                    position.Altitude = buf.ReadShort();
                }
            }

            if (type == MsgGpsLbs3)
            {
                var module = buf.ReadUnsignedShort();
                var subLength = buf.ReadUnsignedByte();
                switch (module)
                {
                    case 0x0027: position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0); break;
                    case 0x002E: position.Set(Position.KeyOdometer, buf.ReadUnsignedInt()); break;
                    case 0x003B: position.Accuracy = buf.ReadUnsignedShort() / 100.0; break;
                    default: buf.SkipBytes(subLength); break;
                }
            }

            if (type == MsgGpsLbsRfid)
            {
                position.Set(Position.KeyDriverUniqueId, ByteBufferUtil.HexDump(buf.ReadSlice(8)));
                buf.ReadUnsignedByte(); // validity
            }

            if (modelNt && (type == MsgGpsLbs2 || type == MsgGpsLbsDriver))
            {
                position.Set(Position.KeyOdometer, buf.ReadUnsignedMedium());
                position.Set(Position.KeyHours, buf.ReadUnsignedMedium() * 60 * 1000L);
            }

            if (modelNt && type == MsgGpsLbsDriver)
            {
                var driverLength = buf.ReadUnsignedByte();
                if (driverLength > 0)
                {
                    buf.ReadUnsignedByte(); // driver identifier type
                    position.Set(Position.KeyDriverUniqueId, ByteBufferUtil.HexDump(buf.ReadSlice(driverLength - 1)));
                }
            }

            if (buf.ReadableBytes == 3 + 6 || buf.ReadableBytes == 3 + 4 + 6)
            {
                position.Set(Position.KeyIgnition, buf.ReadUnsignedByte() > 0);
                buf.ReadUnsignedByte(); // upload mode
                if (buf.ReadUnsignedByte() > 0)
                {
                    position.Set(Position.KeyArchive, true);
                }
            }

            if (buf.ReadableBytes == 4 + 6)
            {
                position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
            }

            if (type == MsgGpsLbsStatus3 || type == MsgFenceMulti)
            {
                position.Set(Position.KeyGeofence, buf.ReadUnsignedByte());
            }

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgAlarm)
        {
            var jc400 = _variant == Variant.Jc400;
            var extendedAlarm = dataLength > 7;
            if (extendedAlarm)
            {
                if (jc400)
                {
                    buf.ReadUnsignedShort(); // marker
                    buf.ReadUnsignedByte(); // version
                }
                DecodeGps(
                    position, buf, false, jc400, jc400, jc400, jc400,
                    deviceSession!.Get<TimeZoneInfo>(DeviceSession.KeyTimezone));
            }
            else
            {
                GetLastLocation(position, DecodeDate(buf, deviceSession!));
            }
            if (_variant == Variant.Jc400)
            {
                position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 10.0);
            }
            var evt = buf.ReadUnsignedByte();
            position.Set(Position.KeyEvent, evt);
            position.Set("eventData", buf.ReadUnsignedShort());
            switch (evt)
            {
                case 0x01: position.AddAlarm(extendedAlarm ? Position.AlarmSos : Position.AlarmGeneral); break;
                case 0x0E: position.AddAlarm(Position.AlarmLowPower); break;
                case 0x76: position.AddAlarm(Position.AlarmTemperature); break;
                case 0x80: position.AddAlarm(Position.AlarmVibration); break;
                case 0x87: position.AddAlarm(Position.AlarmOverspeed); break;
                case 0x88: position.AddAlarm(Position.AlarmPowerCut); break;
                case 0x90: position.AddAlarm(Position.AlarmAcceleration); break;
                case 0x91: position.AddAlarm(Position.AlarmBraking); break;
                case 0x92: position.AddAlarm(Position.AlarmCornering); break;
                case 0x93: position.AddAlarm(Position.AlarmAccident); break;
            }

            var filesLength = buf.ReadableBytes - 6;
            if (filesLength > 0)
            {
                position.Set("eventFiles", buf.ReadString(filesLength, System.Text.Encoding.ASCII));
            }

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgStringInfo)
        {
            buf.ReadUnsignedInt(); // server flag
            string data;
            if (buf.ReadUnsignedByte() == 1)
            {
                data = buf.ReadString(buf.ReadableBytes - 6, System.Text.Encoding.ASCII);
            }
            else
            {
                data = buf.ReadString(buf.ReadableBytes - 6, System.Text.Encoding.BigEndianUnicode);
            }

            var parser = new Parser(PatternLocation, data);

            if (parser.Matches())
            {
                position.Valid = true;
                position.Latitude = parser.NextCoordinate(CoordinateFormat.HemDeg);
                position.Longitude = parser.NextCoordinate(CoordinateFormat.HemDeg);
                position.Course = parser.NextDouble() ?? 0;
                position.Speed = parser.NextDouble() ?? 0;
                position.SetTime(parser.NextDateTime(DateTimeFormat.YmdHms));
            }
            else
            {
                GetLastLocation(position, null);
                position.Set(Position.KeyResult, data);
            }

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgInfo)
        {
            var subType = buf.ReadUnsignedByte();

            GetLastLocation(position, null);

            if (subType == 0x00)
            {
                position.Set(Position.PrefixAdc + 1, buf.ReadUnsignedShort() / 100.0);
                return position;
            }

            if (subType == 0x04)
            {
                var content = buf.ReadString(buf.ReadableBytes - 4 - 2, System.Text.Encoding.ASCII);
                var values = content.Split(';');
                foreach (var value in values)
                {
                    var pair = value.Split('=');
                    if (pair.Length < 2)
                    {
                        continue;
                    }
                    switch (pair[0])
                    {
                        case "ALM1":
                        case "ALM2":
                        case "ALM3":
                        case "ALM4":
                            position.Set("alarm" + pair[0][3] + "Status", Convert.ToInt32(pair[1], 16));
                            break;
                        case "STA1":
                            position.Set("otherStatus", Convert.ToInt32(pair[1], 16));
                            break;
                        case "DYD":
                            position.Set("engineStatus", Convert.ToInt32(pair[1], 16));
                            break;
                    }
                }
                return position;
            }

            if (subType == 0x05)
            {
                if (buf.ReadableBytes >= 6 + 1 + 6)
                {
                    position.DeviceTime = DecodeDate(buf, deviceSession!);
                }

                var flags = buf.ReadUnsignedByte();
                position.Set(Position.KeyDoor, BitUtil.Check(flags, 0));
                position.Set(Position.PrefixIo + 1, BitUtil.Check(flags, 2));
                return position;
            }

            if (subType == 0x0a)
            {
                buf.SkipBytes(8); // imei
                buf.SkipBytes(8); // imsi
                position.Set(Position.KeyIccid, ByteBufferUtil.HexDump(buf.ReadSlice(10)).TrimEnd('f'));
                return position;
            }

            if (subType == 0x0b)
            {
                var contentLength = length - 6;
                if (contentLength == 1)
                {
                    position.Set("networkTechnology", buf.ReadByte() > 0 ? "4G" : "2G");
                }
                else if (contentLength == 2)
                {
                    position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                }
                return position;
            }

            if (subType == 0x0d)
            {
                if (buf.GetByte(buf.ReaderIndex) != '!')
                {
                    buf.SkipBytes(6);
                }

                var parser = new Parser(PatternFuel, buf.ToString(
                    buf.ReaderIndex, buf.ReadableBytes - 4 - 2, System.Text.Encoding.ASCII));
                if (!parser.Matches())
                {
                    return null;
                }

                position.Set(Position.PrefixTemp + 1, parser.NextDouble(0));
                position.Set(Position.KeyFuel, parser.NextDouble(0));

                return position;
            }

            if (subType == 0x1b)
            {
                if (char.IsLetter((char)buf.GetUnsignedByte(buf.ReaderIndex)))
                {
                    var data = buf.ReadString(buf.ReadableBytes - 6, System.Text.Encoding.ASCII);
                    position.Set("serial", data.Trim());
                }
                else
                {
                    buf.ReadUnsignedByte(); // header
                    buf.ReadUnsignedByte(); // type
                    position.Set(Position.KeyDriverUniqueId, ByteBufferUtil.HexDump(buf.ReadSlice(4)));
                    buf.ReadUnsignedByte(); // checksum
                    buf.ReadUnsignedByte(); // footer
                }
                return position;
            }

            if (subType == 0x1e)
            {
                position.Set(Position.PrefixTemp + 1, buf.ReadInt() / 10.0);
                position.Set(Position.KeyHumidity, buf.ReadInt() / 10.0);

                return position;
            }

            return null;
        }

        if (type == MsgX1PhotoData)
        {
            var pictureId = buf.ReadInt();

            var photo = GetMediaBuffer();

            buf.ReadUnsignedInt(); // offset
            var chunk = new byte[buf.ReadUnsignedShort()];
            buf.Inner.ReadBytes(chunk);
            photo?.WriteBytes(chunk);

            if (photo != null && photo.WritableBytes > 0)
            {
                SendPhotoRequest(channel, pictureId);
            }
            else
            {
                position.Set(Position.KeyImage, WriteMediaFile(deviceSession!.UniqueId, "jpg"));
            }

            return null;
        }

        if (type == MsgAz735Gps || type == MsgAz735Alarm)
        {
            if (!DecodeGps(position, buf, true, deviceSession!.Get<TimeZoneInfo>(DeviceSession.KeyTimezone)))
            {
                GetLastLocation(position, position.DeviceTime);
            }

            if (DecodeLbs(position, buf, type, true))
            {
                position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
            }

            buf.SkipBytes(buf.ReadUnsignedByte()); // additional cell towers
            buf.SkipBytes(buf.ReadUnsignedByte()); // wifi access point

            var status = buf.ReadUnsignedByte();
            position.Set(Position.KeyStatus, status);

            if (type == MsgAz735Alarm)
            {
                switch (status)
                {
                    case 0xA0: position.Set(Position.KeyArmed, true); break;
                    case 0xA1: position.Set(Position.KeyArmed, false); break;
                    case 0xA2:
                    case 0xA3: position.AddAlarm(Position.AlarmLowBattery); break;
                    case 0xA4: position.AddAlarm(Position.AlarmGeneral); break;
                    case 0xA5: position.AddAlarm(Position.AlarmDoor); break;
                }
            }

            buf.SkipBytes(buf.ReadUnsignedByte()); // reserved extension

            SendResponse(channel, true, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgObd)
        {
            GetLastLocation(position, DecodeDate(buf, deviceSession!));

            position.Set(Position.KeyIgnition, buf.ReadUnsignedByte() > 0);

            var data = buf.ReadString(buf.ReadableBytes - 18, System.Text.Encoding.ASCII);
            foreach (var pair in data.Split(','))
            {
                var values = pair.Split('=');
                if (values.Length >= 2)
                {
                    switch (Convert.ToInt32(values[0][..2], 16))
                    {
                        case 40: position.Set(Position.KeyOdometer, Convert.ToInt32(values[1], 16) / 100.0); break;
                        case 43: position.Set(Position.KeyFuel, Convert.ToInt32(values[1], 16) / 100.0); break;
                        case 45: position.Set(Position.KeyCoolantTemp, Convert.ToInt32(values[1], 16) / 100.0); break;
                        case 53: position.Set(Position.KeyObdSpeed, Convert.ToInt32(values[1], 16) / 100.0); break;
                        case 54: position.Set(Position.KeyRpm, Convert.ToInt32(values[1], 16) / 100.0); break;
                        case 71: position.Set(Position.KeyFuelUsed, Convert.ToInt32(values[1], 16) / 100.0); break;
                        case 73: position.Set(Position.KeyHours, Convert.ToInt32(values[1], 16) / 100.0); break;
                        case 74: position.Set(Position.KeyVin, values[1]); break;
                    }
                }
            }

            return position;
        }

        if (type == MsgGpsModular)
        {
            while (buf.ReadableBytes > 6)
            {
                var moduleType = buf.ReadUnsignedShort();
                var moduleLength = buf.ReadUnsignedShort();

                switch (moduleType)
                {
                    case 0x03: position.Set(Position.KeyIccid, ByteBufferUtil.HexDump(buf.ReadSlice(10))); break;
                    case 0x09: position.Set(Position.KeySatellites, buf.ReadUnsignedByte()); break;
                    case 0x0a: position.Set(Position.KeySatellitesVisible, buf.ReadUnsignedByte()); break;
                    case 0x11:
                    {
                        var cellTower = CellTower.From(
                            buf.ReadUnsignedShort(), buf.ReadUnsignedShort(), buf.ReadUnsignedShort(),
                            buf.ReadUnsignedMedium(), buf.ReadUnsignedByte());
                        if (cellTower.CellId > 0)
                        {
                            var net = new Network();
                            net.AddCellTower(cellTower);
                            position.Network = net;
                        }
                        break;
                    }
                    case 0x18: position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0); break;
                    case 0x28: position.Set(Position.KeyHdop, buf.ReadUnsignedByte() / 10.0); break;
                    case 0x29: position.Set(Position.KeyIndex, buf.ReadUnsignedInt()); break;
                    case 0x2a:
                    {
                        var input = buf.ReadUnsignedByte();
                        position.Set(Position.KeyDoor, BitUtil.To(input, 4) > 0);
                        position.Set("tamper", BitUtil.From(input, 4) > 0);
                        break;
                    }
                    case 0x2b: position.Set("bootReason", buf.ReadUnsignedByte()); break;
                    case 0x2e: position.Set(Position.KeyOdometer, buf.ReadUnsignedIntLE()); break;
                    case 0x33:
                    {
                        position.SetTime(DateTimeOffset.FromUnixTimeSeconds(buf.ReadUnsignedInt()).UtcDateTime);
                        position.Set(Position.KeySatellites, buf.ReadUnsignedByte());
                        position.Altitude = buf.ReadShort();

                        var latitude = buf.ReadUnsignedInt() / 60.0 / 30000.0;
                        var longitude = buf.ReadUnsignedInt() / 60.0 / 30000.0;
                        position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedByte());

                        var flags = buf.ReadUnsignedShort();
                        position.Course = BitUtil.To(flags, 10);
                        position.Valid = BitUtil.Check(flags, 12);

                        if (!BitUtil.Check(flags, 10))
                        {
                            latitude = -latitude;
                        }
                        if (BitUtil.Check(flags, 11))
                        {
                            longitude = -longitude;
                        }

                        position.Latitude = latitude;
                        position.Longitude = longitude;
                        break;
                    }
                    case 0x34:
                    {
                        var evt = buf.ReadUnsignedByte();
                        switch (evt)
                        {
                            case 0x10: position.AddAlarm(Position.AlarmDoor); break;
                            case 0x12: position.AddAlarm(Position.AlarmRemoving); break;
                            case 0x14: position.AddAlarm(Position.AlarmGeofenceEnter); break;
                            case 0x15: position.AddAlarm(Position.AlarmGeofenceExit); break;
                            case 0x16: position.AddAlarm(Position.AlarmTemperature); break;
                            case 0x1A: position.AddAlarm(Position.AlarmLowBattery); break;
                            case 0x1B: position.AddAlarm(Position.AlarmOverspeed); break;
                            case 0x1C: position.AddAlarm(Position.AlarmPowerOn); break;
                            case 0x1D: position.AddAlarm(Position.AlarmPowerOff); break;
                            case 0x20: position.AddAlarm(Position.AlarmVibration); break;
                            case 0x21: position.AddAlarm(Position.AlarmPowerCut); break;
                            case 0x23: position.AddAlarm(Position.AlarmTampering); break;
                        }
                        position.Set(Position.KeyEvent, evt);
                        buf.ReadUnsignedIntLE(); // time
                        buf.SkipBytes(buf.ReadUnsignedByte()); // content
                        break;
                    }
                    case 0x7f:
                        position.Set("tag1Id", ByteBufferUtil.HexDump(buf.ReadSlice(6)));
                        position.Set("tag1Battery", buf.ReadUnsignedShort() / 1000.0);
                        position.Set("tag1BatteryLevel", buf.ReadUnsignedByte());
                        position.Set("tag1Temp", buf.ReadShort() / 10.0);
                        position.Set("tag1Humidity", buf.ReadShort());
                        position.Set("tag1Status", buf.ReadUnsignedByte());
                        break;
                    default:
                        buf.SkipBytes(moduleLength);
                        break;
                }
            }

            if (position.FixTime == null)
            {
                GetLastLocation(position, null);
            }

            SendResponse(channel, false, MsgGpsModular, buf.ReadUnsignedShort(), null);

            return position;
        }

        if (type == MsgMultimedia)
        {
            buf.SkipBytes(8); // serial number
            var timestamp = buf.ReadUnsignedInt() * 1000;
            buf.SkipBytes(4 + 4 + 2 + 1 + 1 + 2); // gps
            buf.SkipBytes(2 + 2 + 2 + 2); // cell

            buf.ReadInt(); // media id
            var mediaLength = buf.ReadInt();
            var mediaType = buf.ReadUnsignedByte();
            var mediaFormat = buf.ReadUnsignedByte();

            if (mediaType == 0 && mediaFormat == 0)
            {
                buf.ReadUnsignedByte(); // event

                IByteBuffer? photo;
                if (buf.ReadUnsignedShort() == 0)
                {
                    photo = NewMediaBuffer(mediaLength);
                }
                else
                {
                    photo = GetMediaBuffer();
                }

                if (photo != null)
                {
                    var chunk = new byte[buf.ReadableBytes - 3 * 2];
                    buf.Inner.ReadBytes(chunk);
                    photo.WriteBytes(chunk);
                    if (!photo.IsWritable())
                    {
                        GetLastLocation(position, DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime);
                        position.Set(Position.KeyImage, WriteMediaFile(deviceSession!.UniqueId, "jpg"));
                    }
                }
            }

            SendResponse(channel, true, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgSerial)
        {
            GetLastLocation(position, null);

            buf.ReadUnsignedByte(); // external device type code

            var dataLen = buf.ReadableBytes - 6; // index + checksum + footer
            if (BufferUtil.IsPrintable(buf, dataLen))
            {
                var value = buf.ReadString(dataLen, System.Text.Encoding.ASCII);
                position.Set(Position.KeyResult, value.Trim());
            }
            else
            {
                var bytes = new byte[dataLen];
                buf.Inner.ReadBytes(bytes);
                position.Set(Position.KeyResult, Convert.ToHexString(bytes).ToLowerInvariant());
            }

            return position;
        }

        if (type == MsgPeripheral || type == MsgDeviceStatus)
        {
            GetLastLocation(position, DateTimeOffset.FromUnixTimeSeconds(buf.ReadUnsignedInt()).UtcDateTime);

            while (buf.ReadableBytes > 6)
            {
                var statusId = buf.ReadUnsignedShort();
                if (type == MsgPeripheral)
                {
                    switch (statusId)
                    {
                        case 0x000A:
                        {
                            var statusLength = buf.ReadUnsignedShort();
                            buf.ReadUnsignedByte(); // mac address type
                            position.Set("mac", ByteBufferUtil.HexDump(buf.ReadSlice(6)));
                            var evt = buf.ReadUnsignedByte();
                            position.Set(Position.KeyEvent, evt);
                            var eventData = buf.ReadUnsignedByte();
                            if (evt > 0)
                            {
                                position.Set("eventData", eventData);
                            }
                            position.Set("dataType", buf.ReadUnsignedByte());
                            position.Set("dataDetails", ByteBufferUtil.HexDump(buf.ReadSlice(statusLength - 10)));
                            break;
                        }
                        case 0x000C:
                            buf.ReadUnsignedByte(); // length
                            position.Set("externalBatteryLevel", buf.ReadUnsignedByte());
                            if (buf.ReadUnsignedByte() > 0)
                            {
                                position.Set("externalBatteryCharge", true);
                            }
                            position.Set("externalBatteryCycles", buf.ReadUnsignedShort());
                            buf.SkipBytes(6);
                            break;
                        default:
                        {
                            var statusLength = buf.ReadUnsignedByte();
                            buf.SkipBytes(statusLength == 0 ? buf.ReadUnsignedByte() : statusLength);
                            break;
                        }
                    }
                }
                else
                {
                    var statusLength = buf.ReadUnsignedByte();
                    switch (statusId)
                    {
                        case 0x0001:
                        {
                            var motionStatus = buf.ReadUnsignedByte();
                            position.Set(Position.KeyMotion, motionStatus == 0x02 || motionStatus == 0x04);
                            break;
                        }
                        case 0x0002: position.Set(Position.KeyArmed, buf.ReadUnsignedByte() > 0); break;
                        case 0x0004: position.Set(Position.KeyIgnition, buf.ReadUnsignedByte() > 0); break;
                        case 0x0005: position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0); break;
                        case 0x0007: position.Set(Position.KeyCharge, buf.ReadUnsignedByte() > 0); break;
                        case 0x000A: position.Set("chargeVoltage", buf.ReadUnsignedShort()); break;
                        case 0x000B: position.Set("batteryTemp", buf.ReadUnsignedShort() / 10.0); break;
                        default: buf.SkipBytes(statusLength); break;
                    }
                }
            }

            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgGpsLbs8)
        {
            DecodeGps(position, buf, false, deviceSession!.Get<TimeZoneInfo>(DeviceSession.KeyTimezone));

            buf.ReadUnsignedByte(); // data upload mode
            buf.ReadUnsignedByte(); // re-upload

            DecodeLbs(position, buf, type, false);

            return position;
        }

        if (dataLength > 0)
        {
            buf.SkipBytes(dataLength);
        }
        if (!extended && type != MsgCommand0 && type != MsgCommand1 && type != MsgCommand2)
        {
            SendResponse(channel, false, type, buf.GetShort(buf.WriterIndex - 6), null);
        }
        return null;
    }
}
