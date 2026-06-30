using System.Net;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Gt06;

/// <summary>
/// Covers the standard GT06 protocol (login/heartbeat/GPS+LBS+status) used by most generic
/// GT06-clone trackers. Vendor-specific variants (WANWAY, BENWAY, S5, OBD6, SEEWORLD, SL4X, etc.)
/// and photo/multimedia/OBD/BMS extensions are not ported.
/// </summary>
public sealed class Gt06ProtocolDecoder(ConnectionManager connectionManager, ILogger<Gt06ProtocolDecoder> logger)
    : BaseProtocolDecoder("gt06", connectionManager, logger)
{
    public const int MsgLogin = 0x01;
    public const int MsgGps = 0x10;
    public const int MsgGpsLbs6 = 0x11;
    public const int MsgGpsLbs1 = 0x12;
    public const int MsgGpsLbs2 = 0x22;
    public const int MsgGpsLbs3 = 0x37;
    public const int MsgGpsLbs4 = 0x2D;
    public const int MsgStatus = 0x13;
    public const int MsgGpsLbsStatus1 = 0x16;
    public const int MsgGpsLbsStatus2 = 0x26;
    public const int MsgGpsLbsStatus3 = 0x27;
    public const int MsgLbsStatus = 0x19;
    public const int MsgHeartbeat = 0x23;
    public const int MsgAddressRequest = 0x2A;
    public const int MsgAddressResponse = 0x97;
    public const int MsgTimeRequest = 0x8A;
    public const int MsgCommand0 = 0x80;
    public const int MsgCommand1 = 0x81;
    public const int MsgCommand2 = 0x82;

    private static bool HasGps(int type) => type is MsgGps or MsgGpsLbs1 or MsgGpsLbs2 or MsgGpsLbs3 or MsgGpsLbs4
        or MsgGpsLbs6 or MsgGpsLbsStatus1 or MsgGpsLbsStatus2 or MsgGpsLbsStatus3;

    private static bool HasLbs(int type) => type is MsgLbsStatus or MsgGpsLbs1 or MsgGpsLbs2 or MsgGpsLbs3 or MsgGpsLbs4
        or MsgGpsLbs6 or MsgGpsLbsStatus1 or MsgGpsLbsStatus2 or MsgGpsLbsStatus3;

    private static bool HasStatus(int type) => type is MsgStatus or MsgLbsStatus
        or MsgGpsLbsStatus1 or MsgGpsLbsStatus2 or MsgGpsLbsStatus3;

    private static bool IsSupported(int type) => HasGps(type) || HasLbs(type) || HasStatus(type);

    public static void DecodeGps(Position position, ByteBuf buf)
    {
        var dateBuilder = new DateBuilder()
            .SetDate(buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte())
            .SetTime(buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte());
        position.SetTime(dateBuilder.GetDate());

        position.Set(Position.KeySatellites, BitUtil.To(buf.ReadUnsignedByte(), 4));

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

        if (BitUtil.Check(flags, 14))
        {
            position.Set(Position.KeyIgnition, BitUtil.Check(flags, 15));
        }
    }

    private static bool DecodeLbs(Position position, ByteBuf buf, int type, bool hasLength)
    {
        var length = 0;
        if (hasLength)
        {
            length = buf.ReadUnsignedByte();
            if (length == 0)
            {
                return false;
            }
        }

        var mcc = buf.ReadUnsignedShort();
        var mnc = BitUtil.Check(mcc, 15) || type == MsgGpsLbs6 ? buf.ReadUnsignedShort() : buf.ReadUnsignedByte();
        var lac = buf.ReadUnsignedShort();
        var cid = type == MsgGpsLbs6 ? buf.ReadUnsignedInt() : buf.ReadUnsignedMedium();

        var network = new Network();
        network.AddCellTower(CellTower.From(BitUtil.To(mcc, 15), mnc, lac, cid));
        position.Network = network;

        if (length > 9)
        {
            buf.SkipBytes(length - 9);
        }

        return true;
    }

    private static void DecodeStatus(Position position, ByteBuf buf)
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
            case 7: position.AddAlarm(Position.AlarmRemoving); break;
        }
    }

    private static string? DecodeAlarm(int value) => value switch
    {
        0x01 => Position.AlarmSos,
        0x02 => Position.AlarmPowerCut,
        0x03 => Position.AlarmVibration,
        0x04 => Position.AlarmGeofenceEnter,
        0x05 => Position.AlarmGeofenceExit,
        0x06 => Position.AlarmOverspeed,
        0x09 => Position.AlarmVibration,
        0x0E or 0x0F => Position.AlarmLowBattery,
        0x11 => Position.AlarmPowerOff,
        0x0C or 0x13 or 0x25 => Position.AlarmTampering,
        0x14 => Position.AlarmDoor,
        0x18 => Position.AlarmRemoving,
        0x19 => Position.AlarmLowBattery,
        0x1A or 0x27 => Position.AlarmBraking,
        0x1B or 0x2A or 0x2B or 0x2E => Position.AlarmCornering,
        0x23 => Position.AlarmFallDown,
        0x26 => Position.AlarmAcceleration,
        0x28 => Position.AlarmBraking,
        0x29 => Position.AlarmAcceleration,
        0x2C => Position.AlarmAccident,
        0x30 => Position.AlarmJamming,
        _ => null,
    };

    private static void SendResponse(IChannel channel, int type, int index, IByteBuffer? content)
    {
        var response = Unpooled.Buffer();
        var length = 5 + (content?.ReadableBytes ?? 0);
        response.WriteShort(0x7878);
        response.WriteByte(length);
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

    private object? DecodeBasic(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var length = buf.ReadUnsignedByte();
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

        if (type == MsgLogin)
        {
            var imei = ByteBufferUtil.HexDump(buf.ReadSlice(8))[1..];
            buf.ReadUnsignedShort(); // type

            deviceSession = GetDeviceSession(channel, remoteAddress, imei);
            if (deviceSession != null)
            {
                SendResponse(channel, type, buf.GetShort(buf.WriterIndex - 6), null);
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

            SendResponse(channel, type, buf.GetShort(buf.WriterIndex - 6), null);

            return position;
        }

        if (type == MsgAddressRequest)
        {
            const string response = "NA&&NA&&0##";
            var content = Unpooled.Buffer();
            content.WriteByte(response.Length);
            content.WriteInt(0);
            content.WriteBytes(System.Text.Encoding.ASCII.GetBytes(response));
            SendResponse(channel, MsgAddressResponse, 0, content);

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
            SendResponse(channel, MsgTimeRequest, 0, content);

            return null;
        }

        if (IsSupported(type))
        {
            position.Set(Position.KeyType, type);

            if (HasGps(type))
            {
                DecodeGps(position, buf);
            }
            else
            {
                GetLastLocation(position, null);
            }

            if (HasLbs(type) && buf.ReadableBytes > 6)
            {
                var hasLength = HasStatus(type) && type != MsgLbsStatus;
                DecodeLbs(position, buf, type, hasLength);
            }

            if (HasStatus(type))
            {
                DecodeStatus(position, buf);

                var battery = buf.ReadUnsignedByte();
                if (battery <= 6)
                {
                    position.Set(Position.KeyBatteryLevel, battery * 100 / 6);
                }
                else if (battery <= 100)
                {
                    position.Set(Position.KeyBatteryLevel, battery);
                }

                position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                position.AddAlarm(DecodeAlarm(buf.ReadUnsignedByte()));
            }

            if (buf.ReadableBytes == 3 + 6 || buf.ReadableBytes == 3 + 4 + 6)
            {
                position.Set(Position.KeyIgnition, buf.ReadUnsignedByte() > 0);
                buf.ReadByte(); // upload mode
                if (buf.ReadUnsignedByte() > 0)
                {
                    position.Set(Position.KeyArchive, true);
                }
            }

            if (buf.ReadableBytes == 4 + 6)
            {
                position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
            }
        }
        else
        {
            if (dataLength > 0)
            {
                buf.SkipBytes(dataLength);
            }
            if (type is not (MsgCommand0 or MsgCommand1 or MsgCommand2))
            {
                SendResponse(channel, type, buf.GetShort(buf.WriterIndex - 6), null);
            }
            return null;
        }

        SendResponse(channel, type, buf.GetShort(buf.WriterIndex - 6), null);

        return position;
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = new ByteBuf((IByteBuffer)message);
        var header = buf.ReadShort();

        if (header == 0x7878)
        {
            return DecodeBasic(channel, remoteAddress, buf);
        }

        // Extended (0x7979) frames cover photo/multimedia/OBD/GPS-modular messages, not ported.
        return null;
    }
}
