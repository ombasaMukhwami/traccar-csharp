using System.Net;
using System.Text;
using DotNetty.Buffers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Jt808;

/// <summary>
/// JT/T 808 (China's national GPS-tracking protocol standard). Photo/video-attachment requests
/// (Java's requestAttachments, which depends on an unported JimiPhotoProtocol) are not sent.
/// </summary>
public sealed class Jt808ProtocolDecoder(ConnectionManager connectionManager, IConfiguration configuration, ILogger<Jt808ProtocolDecoder> logger)
    : BaseProtocolDecoder("jt808", connectionManager, logger)
{
    public const int MsgTerminalGeneralResponse = 0x0001;
    public const int MsgGeneralResponse = 0x8001;
    public const int MsgGeneralResponse2 = 0x4401;
    public const int MsgHeartbeat = 0x0002;
    public const int MsgTerminalLogout = 0x0003;
    public const int MsgHeartbeat2 = 0x0506;
    public const int MsgTerminalRegister = 0x0100;
    public const int MsgTerminalRegisterResponse = 0x8100;
    public const int MsgTerminalControl = 0x8105;
    public const int MsgTerminalAuth = 0x0102;
    public const int MsgTerminalAttributes = 0x0107;
    public const int MsgLocationReport = 0x0200;
    public const int MsgLocationBatch2 = 0x0210;
    public const int MsgAcceleration = 0x2070;
    public const int MsgLocationReport2 = 0x5501;
    public const int MsgLocationReportBlind = 0x5502;
    public const int MsgLocationBatch = 0x0704;
    public const int MsgOilControl = 0xA006;
    public const int MsgTimeSyncRequest = 0x0109;
    public const int MsgTimeSyncResponse = 0x8109;
    public const int MsgTimezoneSync = 0x1007;
    public const int MsgPhoto = 0x8888;
    public const int MsgTransparent = 0x0900;
    public const int MsgTransparentDownlink = 0x8900;
    public const int MsgParameterSetting = 0x0310;
    public const int MsgSendTextMessage = 0x8300;
    public const int MsgReportTextMessage = 0x6006;
    public const int MsgConfigurationParameters = 0x8103;
    public const int MsgCommandResponse = 0x0701;
    public const int MsgTextMessageResponse = 0x1300;
    public const int MsgDriverIdentity = 0x0702;
    public const int MsgVideoRequest = 0x9101;
    public const int MsgVideoControl = 0x9102;

    public const int ResultSuccess = 0;

    private static readonly HashSet<string> AlarmModelsTamper = ["G-360P", "G-508P"];
    private static readonly HashSet<string> AlarmModelsMovement = ["AL300", "GL100"];
    private static readonly HashSet<string> JcModels = ["JC371", "JC181", "JC182", "JC450", "JC451"];

    private int _delimiter = 0x7e;
    private int? _protocolVersion;

    public int? ProtocolVersion => _protocolVersion;

    public IByteBuffer FormatMessage(int type, IByteBuffer id, bool shortIndex, IByteBuffer data)
    {
        var buf = Unpooled.Buffer();
        buf.WriteByte(_delimiter);
        buf.WriteShort(type);
        var attribute = data.ReadableBytes;
        if (_protocolVersion != null)
        {
            attribute |= 0x4000;
        }
        buf.WriteShort(attribute);
        if (_protocolVersion != null)
        {
            buf.WriteByte(_protocolVersion.Value);
        }
        buf.WriteBytes(id, id.ReaderIndex, id.ReadableBytes);
        if (shortIndex)
        {
            buf.WriteByte(1);
        }
        else
        {
            buf.WriteShort(0);
        }
        buf.WriteBytes(data);
        data.Release();
        var checksumBytes = new byte[buf.ReadableBytes - 1];
        buf.GetBytes(1, checksumBytes);
        buf.WriteByte(Checksum.Xor(checksumBytes));
        buf.WriteByte(_delimiter);
        return buf;
    }

    private void SendGeneralResponse(DotNetty.Transport.Channels.IChannel? channel, EndPoint? remoteAddress, IByteBuffer id, int type, int index)
    {
        if (channel != null)
        {
            var response = Unpooled.Buffer();
            response.WriteShort(index);
            response.WriteShort(type);
            response.WriteByte(ResultSuccess);
            WriteResponse(channel, remoteAddress, FormatMessage(MsgGeneralResponse, id, false, response));
        }
    }

    private void SendGeneralResponse2(DotNetty.Transport.Channels.IChannel? channel, EndPoint? remoteAddress, IByteBuffer id, int type)
    {
        if (channel != null)
        {
            var response = Unpooled.Buffer();
            response.WriteShort(type);
            response.WriteByte(ResultSuccess);
            WriteResponse(channel, remoteAddress, FormatMessage(MsgGeneralResponse2, id, true, response));
        }
    }

    private void DecodeAlarm(Position position, string? model, long value)
    {
        if (model != null && AlarmModelsTamper.Contains(model))
        {
            if (BitUtil.Check(value, 0) || BitUtil.Check(value, 4))
            {
                position.AddAlarm(Position.AlarmRemoving);
            }
            if (BitUtil.Check(value, 1))
            {
                position.AddAlarm(Position.AlarmTampering);
            }
        }
        else if (model != null && AlarmModelsMovement.Contains(model))
        {
            if (BitUtil.Check(value, 16))
            {
                position.AddAlarm(Position.AlarmMovement);
            }
        }
        else
        {
            if (BitUtil.Check(value, 0))
            {
                position.AddAlarm(Position.AlarmSos);
            }
            if (BitUtil.Check(value, 1))
            {
                position.AddAlarm(Position.AlarmOverspeed);
            }
            if (BitUtil.Check(value, 2))
            {
                position.AddAlarm(Position.AlarmFatigueDriving);
            }
            if (BitUtil.Check(value, 5) || BitUtil.Check(value, 6))
            {
                position.AddAlarm(Position.AlarmGpsAntennaCut);
            }
            if (BitUtil.Check(value, 4) || BitUtil.Check(value, 9)
                || BitUtil.Check(value, 10) || BitUtil.Check(value, 11))
            {
                position.AddAlarm(Position.AlarmFault);
            }
            if (BitUtil.Check(value, 7) || BitUtil.Check(value, 18))
            {
                position.AddAlarm(Position.AlarmLowBattery);
            }
            if (BitUtil.Check(value, 8))
            {
                position.AddAlarm(Position.AlarmPowerOff);
            }
            if (BitUtil.Check(value, 15))
            {
                position.AddAlarm(Position.AlarmVibration);
            }
            if (BitUtil.Check(value, 16) || BitUtil.Check(value, 17))
            {
                position.AddAlarm(Position.AlarmTampering);
            }
            if (BitUtil.Check(value, 19))
            {
                position.AddAlarm(Position.AlarmParking);
            }
            if (BitUtil.Check(value, 20))
            {
                position.AddAlarm(Position.AlarmGeofence);
            }
            if (BitUtil.Check(value, 26))
            {
                position.AddAlarm(Position.AlarmGeneral);
            }
            if (BitUtil.Check(value, 28))
            {
                position.AddAlarm(Position.AlarmMovement);
            }
            if (BitUtil.Check(value, 29) || BitUtil.Check(value, 30))
            {
                if (model == null || model != "VL300")
                {
                    position.AddAlarm(Position.AlarmAccident);
                }
            }
        }
    }

    private static int ReadSignedWord(ByteBuf buf)
    {
        var value = buf.ReadUnsignedShort();
        return BitUtil.Check(value, 15) ? -BitUtil.To(value, 15) : BitUtil.To(value, 15);
    }

    private static DateTime ReadDate(ByteBuf buf, TimeZoneInfo? timeZone)
    {
        var dateBuilder = new DateBuilder(timeZone)
            .SetYear(BcdUtil.ReadInteger(buf, 2))
            .SetMonth(BcdUtil.ReadInteger(buf, 2))
            .SetDay(BcdUtil.ReadInteger(buf, 2))
            .SetHour(BcdUtil.ReadInteger(buf, 2))
            .SetMinute(BcdUtil.ReadInteger(buf, 2))
            .SetSecond(BcdUtil.ReadInteger(buf, 2));
        return dateBuilder.GetDate();
    }

    public static string DecodeId(IByteBuffer id)
    {
        var serial = ByteBufferUtil.HexDump(id);
        if (System.Text.RegularExpressions.Regex.IsMatch(serial, "^[0-9]+$"))
        {
            return serial;
        }
        else
        {
            var wrapped = new ByteBuf(id);
            var imei = (long)wrapped.GetUnsignedShort(0);
            imei = (imei << 32) + wrapped.GetUnsignedInt(2);
            return imei + Checksum.Luhn(imei).ToString();
        }
    }

    public static IByteBuffer EncodeId(string uniqueId, int length)
    {
        if (length == 10)
        {
            return Unpooled.WrappedBuffer(Convert.FromHexString(uniqueId.PadLeft(20, '0')));
        }
        if (uniqueId.Length % 2 == 0)
        {
            return Unpooled.WrappedBuffer(Convert.FromHexString(uniqueId));
        }
        else
        {
            var imei = long.Parse(uniqueId[..^1]);
            var buf = Unpooled.Buffer(6);
            buf.WriteShort((int)(imei >> 32));
            buf.WriteInt((int)imei);
            return buf;
        }
    }

    private static void DecodeObdRt(Position position, string data)
    {
        var values = data.Split(',');
        var index = 1; // skip header

        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyPower, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyRpm, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyObdSpeed, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyThrottle, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyEngineLoad, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyCoolantTemp, int.Parse(values[index - 1]));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyFuelConsumption, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture)); // instant
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyFuelConsumption, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture)); // average
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyOdometerTrip, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyObdOdometer, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set("tripFuelUsed", double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
        if (values[index++].Length > 0)
        {
            position.Set(Position.KeyFuelUsed, double.Parse(values[index - 1], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    protected override object? Decode(DotNetty.Transport.Channels.IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = (IByteBuffer)message;

        if (buf.GetByte(buf.ReaderIndex) == '(')
        {
            var sentence = buf.ToString(Encoding.Latin1);
            if (sentence.Contains("BASE,2"))
            {
                var response = sentence.Replace("TIME", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                WriteResponse(channel, remoteAddress, Unpooled.CopiedBuffer(response, Encoding.ASCII));
                return null;
            }
            else
            {
                return DecodeResult(channel, remoteAddress, sentence);
            }
        }

        _delimiter = buf.ReadByte();
        var type = buf.ReadUnsignedShort();
        var attribute = buf.ReadUnsignedShort();

        var bodyLength = BitUtil.To(attribute, 10);

        _protocolVersion = BitUtil.Check(attribute, 14) ? buf.ReadByte() : null;
        var id = buf.ReadSlice(_protocolVersion != null ? 10 : (_delimiter == 0xe7 ? 7 : 6));

        int index;
        if (type == MsgLocationReport2 || type == MsgLocationReportBlind)
        {
            index = buf.ReadByte();
        }
        else
        {
            index = buf.ReadUnsignedShort();
        }

        var uniqueId = DecodeId(id);
        var strippedId = StringUtil.StripLeading('0', uniqueId);
        var deviceSession = uniqueId == strippedId
            ? GetDeviceSession(channel, remoteAddress, uniqueId)
            : GetDeviceSession(channel, remoteAddress, strippedId, uniqueId);
        if (deviceSession == null)
        {
            return null;
        }

        if (!deviceSession.Contains(DeviceSession.KeyTimezone))
        {
            deviceSession.Set(DeviceSession.KeyTimezone, GetTimeZone("GMT+8"));
        }

        var wrapped = new ByteBuf(buf);

        if (type == MsgTerminalRegister)
        {
            if (channel != null)
            {
                var response = Unpooled.Buffer();
                response.WriteShort(index);
                response.WriteByte(ResultSuccess);
                response.WriteBytes(Encoding.ASCII.GetBytes(DecodeId(id)));
                WriteResponse(channel, remoteAddress, FormatMessage(MsgTerminalRegisterResponse, id, false, response));
            }
        }
        else if (type == MsgReportTextMessage)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);

            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);

            wrapped.ReadUnsignedByte(); // encoding
            var charset = GetGbkOrAscii();

            position.Set(Position.KeyResult, wrapped.ReadString(wrapped.ReadableBytes - 2, charset));

            return position;
        }
        else if (type == MsgTextMessageResponse)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);

            wrapped.ReadUnsignedShort(); // response serial number

            var result = JavaString.Trim(wrapped.ReadString(bodyLength - 2, Encoding.BigEndianUnicode));
            position.Set(Position.KeyResult, result);

            return position;
        }
        else if (type == MsgHeartbeat)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);

            if (wrapped.ReadableBytes >= 3 + 1)
            {
                var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
                GetLastLocation(position, null);

                position.Set(Position.KeyBatteryLevel, wrapped.ReadUnsignedByte());
                position.Set(Position.KeyRssi, wrapped.ReadUnsignedByte());
                position.Set(Position.KeyStatus, wrapped.ReadUnsignedByte());

                return position;
            }
        }
        else if (type == MsgTerminalAuth || type == MsgHeartbeat2
            || type == MsgPhoto || type == MsgTerminalLogout || type == MsgTimezoneSync)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);
        }
        else if (type == MsgTerminalAttributes)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);

            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);

            wrapped.ReadUnsignedShort(); // terminal type
            wrapped.SkipBytes(5); // manufacturer id
            wrapped.SkipBytes(20); // terminal model
            wrapped.SkipBytes(7); // terminal id

            position.Set(Position.KeyIccid, ByteBufferUtil.HexDump(wrapped.ReadSlice(10)));

            return position;
        }
        else if (type == MsgLocationReport)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);

            var position = DecodeLocation(deviceSession, wrapped);
            return position;
        }
        else if (type == MsgLocationReport2 || type == MsgLocationReportBlind)
        {
            if (BitUtil.Check(attribute, 15))
            {
                SendGeneralResponse2(channel, remoteAddress, id, type);
            }

            return DecodeLocation2(deviceSession, wrapped, type);
        }
        else if (type == MsgLocationBatch2 && bodyLength == 7)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);

            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            position.Set(Position.KeyBatteryLevel, wrapped.ReadUnsignedByte());
            GetLastLocation(position, ReadDate(wrapped, deviceSession.Get<TimeZoneInfo>(DeviceSession.KeyTimezone)));

            return position;
        }
        else if (type == MsgLocationBatch || type == MsgLocationBatch2)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);

            return DecodeLocationBatch(deviceSession, wrapped, type);
        }
        else if (type == MsgTimeSyncRequest)
        {
            if (channel != null)
            {
                var now = DateTime.UtcNow;
                var response = Unpooled.Buffer();
                response.WriteShort(now.Year);
                response.WriteByte(now.Month);
                response.WriteByte(now.Day);
                response.WriteByte(now.Hour);
                response.WriteByte(now.Minute);
                response.WriteByte(now.Second);
                response.WriteByte(ResultSuccess);
                WriteResponse(channel, remoteAddress, FormatMessage(MsgTimeSyncResponse, id, false, response));
            }
        }
        else if (type == MsgAcceleration)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);

            var data = new StringBuilder("[");
            while (wrapped.ReadableBytes > 2)
            {
                wrapped.SkipBytes(6); // time
                if (data.Length > 1)
                {
                    data.Append(',');
                }
                data.Append('[');
                data.Append(ReadSignedWord(wrapped));
                data.Append(',');
                data.Append(ReadSignedWord(wrapped));
                data.Append(',');
                data.Append(ReadSignedWord(wrapped));
                data.Append(']');
            }
            data.Append(']');

            position.Set(Position.KeyGSensor, data.ToString());

            return position;
        }
        else if (type == MsgTransparent)
        {
            SendGeneralResponse(channel, remoteAddress, id, type, index);

            return DecodeTransparent(deviceSession, wrapped);
        }
        else if (type == MsgCommandResponse)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);

            var result = wrapped.ReadString(wrapped.ReadInt(), Encoding.Latin1);
            position.Set(Position.KeyResult, result);

            return position;
        }
        else if (type == MsgDriverIdentity)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);

            position.Set("cardStatus", wrapped.ReadUnsignedByte());

            position.SetTime(ReadDate(wrapped, deviceSession.Get<TimeZoneInfo>(DeviceSession.KeyTimezone)));

            position.Set("cardResult", wrapped.ReadUnsignedByte());
            position.Set("driver", wrapped.ReadString(wrapped.ReadUnsignedByte(), Encoding.Latin1));
            position.Set("cardCode", JavaString.Trim(wrapped.ReadString(20, Encoding.Latin1)));
            position.Set("cardAgency", wrapped.ReadString(wrapped.ReadUnsignedByte(), Encoding.Latin1));
            position.Set("cardValidity", ByteBufferUtil.HexDump(wrapped.ReadSlice(4)));

            return position;
        }

        return null;
    }

    private static Encoding GetGbkOrAscii()
    {
        try
        {
            return Encoding.GetEncoding("GBK");
        }
        catch (ArgumentException)
        {
            return Encoding.ASCII;
        }
    }

    private Position? DecodeResult(DotNetty.Transport.Channels.IChannel channel, EndPoint? remoteAddress, string sentence)
    {
        var deviceSession = GetDeviceSession(channel, remoteAddress);
        if (deviceSession != null)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);
            position.Set(Position.KeyResult, sentence);
            return position;
        }
        return null;
    }

    private void DecodeExtension(Position position, ByteBuf buf, int endIndex)
    {
        while (buf.ReaderIndex < endIndex)
        {
            var type = buf.ReadUnsignedByte();
            var length = buf.ReadUnsignedByte();
            switch (type)
            {
                case 0x01: position.Set(Position.KeyOdometer, buf.ReadUnsignedInt() * 100L); break;
                case 0x02: position.Set(Position.KeyFuel, buf.ReadUnsignedShort() / 10.0); break;
                case 0x03: position.Set(Position.KeyObdSpeed, buf.ReadUnsignedShort() / 10.0); break;
                case 0x56:
                    buf.ReadUnsignedByte(); // power level
                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                    break;
                case 0x61: position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0); break;
                case 0x69: position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0); break;
                case 0x80: position.Set(Position.KeyObdSpeed, buf.ReadUnsignedByte()); break;
                case 0x81: position.Set(Position.KeyRpm, buf.ReadUnsignedShort()); break;
                case 0x82: position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 10.0); break;
                case 0x83: position.Set(Position.KeyEngineLoad, buf.ReadUnsignedByte()); break;
                case 0x84: position.Set(Position.KeyCoolantTemp, buf.ReadUnsignedByte() - 40); break;
                case 0x85: position.Set(Position.KeyFuelConsumption, buf.ReadUnsignedShort()); break;
                case 0x86: position.Set("intakeTemp", buf.ReadUnsignedByte() - 40); break;
                case 0x87: position.Set("intakeFlow", buf.ReadUnsignedShort()); break;
                case 0x88: position.Set("intakePressure", buf.ReadUnsignedByte()); break;
                case 0x89: position.Set(Position.KeyThrottle, buf.ReadUnsignedByte()); break;
                case 0x8B: position.Set(Position.KeyVin, buf.ReadString(17, Encoding.Latin1)); break;
                case 0x8C: position.Set(Position.KeyObdOdometer, buf.ReadUnsignedInt() * 100L); break;
                case 0x8D: position.Set(Position.KeyOdometerTrip, buf.ReadUnsignedShort() * 1000L); break;
                case 0x8E: position.Set(Position.KeyFuel, buf.ReadUnsignedByte()); break;
                case 0xA0:
                    var codes = buf.ReadString(length, Encoding.Latin1);
                    position.Set(Position.KeyDtcs, codes.Replace(',', ' '));
                    break;
                case 0xCC: position.Set(Position.KeyIccid, buf.ReadString(20, Encoding.Latin1)); break;
                default: buf.SkipBytes(length); break;
            }
        }
    }

    private void DecodeCoordinates(Position position, DeviceSession deviceSession, ByteBuf buf)
    {
        var status = buf.ReadInt();

        var model = GetDeviceModel(deviceSession);

        position.Set(Position.KeyIgnition, BitUtil.Check(status, 0));
        if (model == "G1C Pro")
        {
            position.Set(Position.KeyMotion, BitUtil.Check(status, 4));
        }
        position.Set(Position.KeyBlocked, BitUtil.Check(status, 10));
        if (model == "MV810G" || model == "MV710G")
        {
            position.Set(Position.KeyDoor, BitUtil.Check(status, 16));
        }
        position.Set(Position.KeyCharge, BitUtil.Check(status, 26));

        position.Valid = BitUtil.Check(status, 1);

        var lat = buf.ReadUnsignedInt() / 1000000.0;
        var lon = buf.ReadUnsignedInt() / 1000000.0;

        position.Latitude = BitUtil.Check(status, 2) ? -lat : lat;
        position.Longitude = BitUtil.Check(status, 3) ? -lon : lon;
    }

    private static double DecodeCustomDouble(ByteBuf buf)
    {
        int b1 = buf.ReadByte();
        var b2 = buf.ReadUnsignedByte();
        var sign = b1 != 0 ? b1 / Math.Abs(b1) : 1;
        return sign * (Math.Abs(b1) + b2 / 255.0);
    }

    private Position DecodeLocation(DeviceSession deviceSession, ByteBuf buf)
    {
        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        var model = GetDeviceModel(deviceSession);

        DecodeAlarm(position, model, buf.ReadUnsignedInt());

        DecodeCoordinates(position, deviceSession, buf);

        position.Altitude = buf.ReadShort();
        position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedShort() / 10.0);
        position.Course = buf.ReadUnsignedShort();
        position.SetTime(ReadDate(buf, deviceSession.Get<TimeZoneInfo>(DeviceSession.KeyTimezone)));

        if (buf.ReadableBytes == 20)
        {
            buf.SkipBytes(4); // remaining battery and mileage
            position.Set(Position.KeyOdometer, buf.ReadUnsignedInt() * 1000);
            position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 10.0);
            buf.ReadUnsignedInt(); // area id
            position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
            buf.SkipBytes(3); // reserved

            return position;
        }

        var network = new Network();

        while (buf.ReadableBytes > 2)
        {
            var subtype = buf.ReadUnsignedByte();
            var length = buf.ReadUnsignedByte();
            var endIndex = buf.ReaderIndex + length;
            string stringValue;
            int eventValue;
            long alarm;
            switch (subtype)
            {
                case 0x01:
                    position.Set(Position.KeyOdometer, buf.ReadUnsignedInt() * 100);
                    break;
                case 0x02:
                    var fuel = buf.ReadUnsignedShort();
                    if (BitUtil.Check(fuel, 15))
                    {
                        position.Set(Position.KeyFuel, BitUtil.To(fuel, 15));
                    }
                    else
                    {
                        position.Set(Position.KeyFuel, fuel / 10.0);
                    }
                    break;
                case 0x06:
                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                    break;
                case 0x0B:
                    position.Set("lockCommand", buf.ReadUnsignedByte());
                    if (length is >= 5 and <= 6)
                    {
                        position.Set("lockCard", buf.ReadUnsignedInt());
                    }
                    else if (length >= 7)
                    {
                        position.Set("lockPassword", buf.ReadString(6, Encoding.Latin1));
                    }
                    if (length % 2 == 0)
                    {
                        position.Set("unlockResult", buf.ReadUnsignedByte());
                    }
                    break;
                case 0x14:
                    position.Set("videoAlarm", buf.ReadUnsignedInt());
                    break;
                case 0x25:
                    position.Set(Position.KeyInput, buf.ReadUnsignedInt());
                    break;
                case 0x2B:
                case 0xA7:
                    position.Set(Position.PrefixAdc + 1, buf.ReadUnsignedShort() / 100.0);
                    position.Set(Position.PrefixAdc + 2, buf.ReadUnsignedShort() / 100.0);
                    break;
                case 0x30:
                    position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                    break;
                case 0x31:
                    position.Set(Position.KeySatellites, buf.ReadUnsignedByte());
                    break;
                case 0x33:
                    if (length == 1)
                    {
                        position.Set("mode", buf.ReadUnsignedByte());
                    }
                    else
                    {
                        stringValue = buf.ReadString(length, Encoding.Latin1);
                        if (stringValue.StartsWith("*M00"))
                        {
                            var lockStatus = stringValue.Substring(8, 7);
                            position.Set(Position.KeyBattery, int.Parse(lockStatus.Substring(2, 3)) / 100.0);
                        }
                    }
                    break;
                case 0x51:
                    if (length == 2 || length == 16)
                    {
                        for (var i = 1; i <= length / 2; i++)
                        {
                            var value = buf.ReadUnsignedShort();
                            if (value != 0xffff)
                            {
                                if (BitUtil.Check(value, 15))
                                {
                                    position.Set(Position.PrefixTemp + i, -BitUtil.To(value, 15) / 10.0);
                                }
                                else
                                {
                                    position.Set(Position.PrefixTemp + i, value / 10.0);
                                }
                            }
                        }
                    }
                    break;
                case 0x56:
                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte() * 10);
                    buf.ReadUnsignedByte(); // reserved
                    break;
                case 0x57:
                    alarm = buf.ReadUnsignedShort();
                    position.AddAlarm(BitUtil.Check(alarm, 8) ? Position.AlarmAcceleration : null);
                    position.AddAlarm(BitUtil.Check(alarm, 9) ? Position.AlarmBraking : null);
                    position.AddAlarm(BitUtil.Check(alarm, 10) ? Position.AlarmCornering : null);
                    buf.ReadUnsignedShort(); // external switch state
                    alarm = buf.ReadUnsignedInt();
                    if (model == "MV810G" || model == "MV710G")
                    {
                        position.AddAlarm(BitUtil.Check(alarm, 16) ? Position.AlarmDoor : null);
                    }
                    break;
                case 0x60:
                    eventValue = buf.ReadUnsignedShort();
                    position.Set(Position.KeyEvent, eventValue);
                    if (eventValue is >= 0x0061 and <= 0x0066)
                    {
                        buf.SkipBytes(6); // lock id
                        stringValue = buf.ReadString(8, Encoding.Latin1);
                        position.Set(Position.KeyDriverUniqueId, stringValue);
                    }
                    break;
                case 0x63:
                    for (var i = 1; i <= length / 11; i++)
                    {
                        position.Set("lock" + i + "Id", ByteBufferUtil.HexDump(buf.ReadSlice(6)));
                        position.Set("lock" + i + "Battery", buf.ReadUnsignedShort() / 1000.0);
                        position.Set("lock" + i + "Seal", buf.ReadUnsignedByte() == 0x31);
                        buf.ReadUnsignedByte(); // physical state
                        buf.ReadUnsignedByte(); // rssi
                    }
                    break;
                case 0x64:
                case 0x65:
                    buf.ReadUnsignedInt(); // alarm serial number
                    buf.ReadUnsignedByte(); // alarm status
                    position.Set(subtype == 0x64 ? "adasAlarm" : "dmsAlarm", buf.ReadUnsignedByte());
                    if (length >= 47)
                    {
                        buf.ReaderIndex = endIndex - 16;
                        position.Set("alarmLabel", ByteBufferUtil.HexDump(buf.ReadSlice(16)));
                    }
                    break;
                case 0x67:
                    stringValue = buf.ReadString(8, Encoding.Latin1);
                    position.Set("password", stringValue);
                    break;
                case 0x70:
                    buf.ReadUnsignedInt(); // alarm serial number
                    buf.ReadUnsignedByte(); // alarm status
                    switch (buf.ReadUnsignedByte())
                    {
                        case 0x01: position.AddAlarm(Position.AlarmAcceleration); break;
                        case 0x02: position.AddAlarm(Position.AlarmBraking); break;
                        case 0x03: position.AddAlarm(Position.AlarmCornering); break;
                        case 0x16: position.AddAlarm(Position.AlarmAccident); break;
                    }
                    break;
                case 0x68:
                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedShort() / 100.0);
                    break;
                case 0x69:
                    position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0);
                    break;
                case 0x77:
                    while (buf.ReaderIndex < endIndex)
                    {
                        var tireIndex = buf.ReadUnsignedByte();
                        position.Set("tire" + tireIndex + "SensorId", ByteBufferUtil.HexDump(buf.ReadSlice(3)));
                        position.Set("tire" + tireIndex + "Pressure", BitUtil.To(buf.ReadUnsignedShort(), 10) / 40.0);
                        position.Set("tire" + tireIndex + "Temp", buf.ReadUnsignedByte() - 50);
                        position.Set("tire" + tireIndex + "Status", buf.ReadUnsignedByte());
                    }
                    break;
                case 0x80:
                    buf.ReadUnsignedByte(); // content
                    endIndex = buf.WriterIndex - 2;
                    DecodeExtension(position, buf, endIndex);
                    break;
                case 0x82:
                    position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 10.0);
                    break;
                case 0x91:
                    position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 10.0);
                    position.Set(Position.KeyRpm, buf.ReadUnsignedShort());
                    position.Set(Position.KeyObdSpeed, buf.ReadUnsignedByte());
                    position.Set(Position.KeyThrottle, buf.ReadUnsignedByte() * 100 / 255);
                    position.Set(Position.KeyEngineLoad, buf.ReadUnsignedByte() * 100 / 255);
                    position.Set(Position.KeyCoolantTemp, buf.ReadUnsignedByte() - 40);
                    buf.ReadUnsignedShort();
                    position.Set(Position.KeyFuelConsumption, buf.ReadUnsignedShort() / 100.0);
                    buf.ReadUnsignedShort();
                    buf.ReadUnsignedInt();
                    buf.ReadUnsignedShort();
                    position.Set(Position.KeyFuelUsed, buf.ReadUnsignedShort() / 100.0);
                    break;
                case 0x94:
                    if (length > 0)
                    {
                        stringValue = buf.ReadString(length, Encoding.Latin1);
                        position.Set(Position.KeyVin, stringValue);
                    }
                    break;
                case 0xAC:
                    position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
                    break;
                case 0xBC:
                    stringValue = buf.ReadString(length, Encoding.Latin1);
                    position.Set("driver", JavaString.Trim(stringValue));
                    break;
                case 0xBD:
                    stringValue = buf.ReadString(length, Encoding.Latin1);
                    position.Set(Position.KeyDriverUniqueId, stringValue);
                    break;
                case 0xD0:
                    var userStatus = buf.ReadUnsignedInt();
                    if (BitUtil.Check(userStatus, 3))
                    {
                        position.AddAlarm(Position.AlarmVibration);
                    }
                    break;
                case 0xD3:
                    position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 10.0);
                    break;
                case 0xD4:
                case 0xE1:
                case 0xE9:
                    if (length == 1)
                    {
                        var batteryLevel = buf.ReadUnsignedByte();
                        if (batteryLevel == 0xff)
                        {
                            position.Set(Position.KeyCharge, true);
                        }
                        else
                        {
                            position.Set(Position.KeyBatteryLevel, batteryLevel);
                        }
                    }
                    else if (subtype == 0xE1 && length >= 12 && (length - 4) % 8 == 0)
                    {
                        var mcc = buf.ReadUnsignedShort();
                        var mnc = buf.ReadUnsignedShort();
                        while (buf.ReaderIndex < endIndex)
                        {
                            network.AddCellTower(CellTower.From(
                                mcc, mnc, buf.ReadUnsignedMedium(), buf.ReadUnsignedInt(), buf.ReadUnsignedByte()));
                        }
                    }
                    else if (subtype == 0xE1 && length == 2)
                    {
                        position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 10.0);
                    }
                    else
                    {
                        position.Set(Position.KeyDriverUniqueId, buf.ReadUnsignedInt().ToString());
                    }
                    break;
                case 0xD5:
                    if (length == 2)
                    {
                        position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0);
                    }
                    else
                    {
                        var count = buf.ReadUnsignedByte();
                        for (var i = 1; i <= count; i++)
                        {
                            position.Set("lock" + i + "Id", ByteBufferUtil.HexDump(buf.ReadSlice(5)));
                            position.Set("lock" + i + "Card", ByteBufferUtil.HexDump(buf.ReadSlice(5)));
                            position.Set("lock" + i + "Battery", buf.ReadUnsignedByte());
                            var status = buf.ReadUnsignedShort();
                            position.Set("lock" + i + "Locked", !BitUtil.Check(status, 5));
                        }
                    }
                    break;
                case 0xDA:
                    buf.ReadUnsignedShort(); // string cut count
                    var deviceStatus = buf.ReadUnsignedByte();
                    position.Set("string", BitUtil.Check(deviceStatus, 0));
                    position.Set(Position.KeyMotion, BitUtil.Check(deviceStatus, 2));
                    position.Set("cover", BitUtil.Check(deviceStatus, 3));
                    break;
                case 0xE2:
                    if (model != "DT800")
                    {
                        position.Set(Position.KeyFuel, buf.ReadUnsignedInt() / 10.0);
                    }
                    break;
                case 0xE3:
                    buf.ReadUnsignedByte(); // reserved
                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                    position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0);
                    break;
                case 0xE4:
                    if (buf.ReadUnsignedByte() == 0)
                    {
                        position.Set(Position.KeyCharge, true);
                    }
                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                    break;
                case 0xE5:
                    if (length == 1)
                    {
                        position.Set(Position.KeyMotion, buf.ReadUnsignedByte() == 1);
                    }
                    break;
                case 0xE6:
                    if (length >= 7 && buf.GetString(buf.ReaderIndex, 7, Encoding.UTF8) == "$OBD-RT")
                    {
                        var data = buf.ReadString(length, Encoding.UTF8);
                        DecodeObdRt(position, data);
                    }
                    else if (length >= 11 && length % 11 == 0)
                    {
                        while (buf.ReaderIndex < endIndex)
                        {
                            var sensorIndex = buf.ReadUnsignedByte();
                            buf.SkipBytes(6); // mac
                            position.Set(Position.PrefixTemp + sensorIndex, DecodeCustomDouble(buf));
                            position.Set("humidity" + sensorIndex, DecodeCustomDouble(buf));
                        }
                    }
                    break;
                case 0xE7:
                    if (length == 8)
                    {
                        alarm = buf.ReadUnsignedShort();
                        position.AddAlarm(BitUtil.Check(alarm, 0) ? Position.AlarmVibration : null);
                        position.AddAlarm(BitUtil.Between(alarm, 1, 4) != 0 ? Position.AlarmSos : null);
                    }
                    break;
                case 0xE8:
                    if (model != null && JcModels.Contains(model))
                    {
                        var extendedType = buf.ReadUnsignedShort();
                        switch (extendedType)
                        {
                            case 0x2002:
                                buf.ReadUnsignedByte(); // packet length
                                position.Set("uploadMode", buf.ReadUnsignedByte());
                                position.Set("buffered", buf.ReadUnsignedByte() == 1);
                                break;
                            case 0x2003:
                                buf.ReadUnsignedByte(); // packet length
                                position.Set(Position.KeyCharge, buf.ReadUnsignedByte() == 1);
                                var batteryStatus = buf.ReadUnsignedByte();
                                if (batteryStatus is >= 1 and <= 6)
                                {
                                    position.Set(Position.KeyBatteryLevel, batteryStatus * 100 / 6);
                                }
                                position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0);
                                position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                                position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                                break;
                            case 0x2005:
                                buf.ReadUnsignedByte(); // packet length
                                while (buf.ReaderIndex < endIndex)
                                {
                                    var statusId = buf.ReadUnsignedShort();
                                    var statusLength = buf.ReadUnsignedByte();
                                    var statusEndIndex = buf.ReaderIndex + statusLength;
                                    switch (statusId)
                                    {
                                        case 0x0001: position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 100.0); break;
                                        case 0x0002: position.Set("dataUsage", buf.ReadUnsignedInt()); break;
                                        case 0x0003: position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte()); break;
                                        case 0x0004: position.Set(Position.KeyCharge, buf.ReadUnsignedByte() == 0); break;
                                        case 0x0005: position.Set(Position.KeyIccid, ByteBufferUtil.HexDump(buf.ReadSlice(10)).Substring(0, 19)); break;
                                        case 0x0007: position.Set(Position.KeyHdop, buf.ReadUnsignedShort() / 10.0); break;
                                        case 0x2001: position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 10.0); break;
                                    }
                                    buf.ReaderIndex = statusEndIndex;
                                }
                                break;
                            case 0x0001: position.AddAlarm(Position.AlarmFault); break;
                            case 0x0400: case 0x0412: position.AddAlarm(Position.AlarmAcceleration); break;
                            case 0x0401: case 0x0413: position.AddAlarm(Position.AlarmBraking); break;
                            case 0x0402: case 0x0414: position.AddAlarm(Position.AlarmCornering); break;
                            case 0x0403: position.AddAlarm(Position.AlarmOverspeed); break;
                            case 0x0405: case 0x0416: case 0x0417: position.AddAlarm(Position.AlarmAccident); break;
                            case 0x0406: position.AddAlarm(Position.AlarmVibration); break;
                            case 0x0407: position.AddAlarm(Position.AlarmTow); break;
                            case 0x0408: position.AddAlarm(Position.AlarmGeofenceEnter); break;
                            case 0x0409: position.AddAlarm(Position.AlarmGeofenceExit); break;
                            case 0x040C: position.AddAlarm(Position.AlarmDoor); break;
                            case 0x0415: position.AddAlarm(Position.AlarmLaneChange); break;
                            case 0x0C01: position.AddAlarm(Position.AlarmSos); break;
                            case 0x0C02: position.AddAlarm(Position.AlarmLowPower); break;
                            case 0x0C03: position.Set(Position.KeyIgnition, true); break;
                            case 0x0C04: position.Set(Position.KeyIgnition, false); break;
                            case 0x0C05: position.AddAlarm(Position.AlarmGeneral); break;
                            case 0x0C0E: position.AddAlarm(Position.AlarmPowerCut); break;
                            case 0x0C0F: position.AddAlarm(Position.AlarmLowBattery); break;
                            case 0x0C10: position.AddAlarm(Position.AlarmPowerOff); break;
                            case 0x0C12: position.AddAlarm(Position.AlarmTampering); break;
                        }
                        buf.ReaderIndex = endIndex;
                    }
                    else
                    {
                        position.Set("lockStatus", buf.ReadUnsignedMedium());
                    }
                    break;
                case 0xEA:
                    if (length > 2)
                    {
                        buf.ReadUnsignedByte(); // extended info type
                        while (buf.ReaderIndex < endIndex)
                        {
                            var extendedType = buf.ReadUnsignedByte();
                            var extendedLength = buf.ReadUnsignedByte();
                            var extendedEndIndex = buf.ReaderIndex + extendedLength;
                            switch (extendedType)
                            {
                                case 0x11:
                                    position.Set("externalAlarms", buf.ReadUnsignedShort());
                                    position.Set("alarmThresholdType", buf.ReadUnsignedByte());
                                    buf.ReadUnsignedInt(); // upper threshold
                                    buf.ReadUnsignedInt(); // current value
                                    buf.ReadUnsignedInt(); // lower threshold
                                    break;
                                case 0x13:
                                    position.Set("externalIlluminance", buf.ReadUnsignedShort());
                                    break;
                                case 0x14:
                                    position.Set("externalAirPressure", buf.ReadUnsignedShort());
                                    break;
                                case 0x15:
                                    position.Set("externalHumidity", buf.ReadUnsignedShort() / 10.0);
                                    break;
                                case 0x16:
                                    position.Set("externalTemp", buf.ReadUnsignedShort() / 10.0 - 50);
                                    break;
                            }
                            buf.ReaderIndex = extendedEndIndex;
                        }
                    }
                    break;
                case 0xEB:
                    if (buf.GetUnsignedShort(buf.ReaderIndex) > 200 && (length - 3) % 5 == 0)
                    {
                        var mcc = buf.ReadUnsignedShort();
                        var mnc = buf.ReadUnsignedByte();
                        while (buf.ReaderIndex < endIndex)
                        {
                            network.AddCellTower(CellTower.From(
                                mcc, mnc, buf.ReadUnsignedShort(), buf.ReadUnsignedShort(), buf.ReadUnsignedByte()));
                        }
                    }
                    else if (BufferUtil.IsPrintable(buf, length))
                    {
                        position.Set("timezone", buf.ReadString(length, Encoding.UTF8));
                    }
                    else
                    {
                        while (buf.ReaderIndex < endIndex)
                        {
                            var extendedLength = buf.ReadUnsignedShort();
                            var extendedEndIndex = buf.ReaderIndex + extendedLength;
                            var extendedType = buf.ReadUnsignedShort();
                            switch (extendedType)
                            {
                                case 0x0001:
                                    position.Set("fuel1", buf.ReadUnsignedShort() / 10.0);
                                    buf.ReadUnsignedByte(); // unused
                                    break;
                                case 0x0023:
                                    position.Set("fuel2", double.Parse(buf.ReadString(6, Encoding.Latin1), System.Globalization.CultureInfo.InvariantCulture));
                                    break;
                                case 0x002D:
                                    if (extendedLength == 6)
                                    {
                                        position.Set(Position.KeyPower, buf.ReadUnsignedInt() / 1000.0);
                                    }
                                    else
                                    {
                                        position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 1000.0);
                                    }
                                    break;
                                case 0x0089:
                                    alarm = buf.ReadUnsignedInt();
                                    if (!BitUtil.Check(alarm, 0))
                                    {
                                        position.AddAlarm(Position.AlarmPowerOff);
                                    }
                                    if (!BitUtil.Check(alarm, 12))
                                    {
                                        position.AddAlarm(Position.AlarmRemoving);
                                    }
                                    break;
                                case 0x00B2:
                                    position.Set(Position.KeyIccid, StringUtil.StripTrailing('f', ByteBufferUtil.HexDump(buf.ReadSlice(10))));
                                    break;
                                case 0x00B9:
                                    buf.ReadUnsignedByte(); // count
                                    var wifi = buf.ReadString(extendedLength - 3, Encoding.Latin1).Split(',');
                                    for (var i = 0; i < wifi.Length / 2; i++)
                                    {
                                        network.AddWifiAccessPoint(WifiAccessPoint.From(wifi[i * 2], int.Parse(wifi[i * 2 + 1])));
                                    }
                                    break;
                                case 0x00C5:
                                    alarm = buf.ReadUnsignedInt();
                                    if (!BitUtil.Check(alarm, 6))
                                    {
                                        position.AddAlarm(Position.AlarmVibration);
                                    }
                                    var motionState = BitUtil.Between(alarm, 23, 25);
                                    if (motionState == 0)
                                    {
                                        position.Set(Position.KeyMotion, true);
                                    }
                                    else if (motionState == 1)
                                    {
                                        position.Set(Position.KeyMotion, false);
                                    }
                                    break;
                                case 0x00C6:
                                    var batteryAlarm = buf.ReadUnsignedByte();
                                    if (batteryAlarm == 0x03 || batteryAlarm == 0x04)
                                    {
                                        position.Set(Position.KeyAlarm, Position.AlarmLowBattery);
                                    }
                                    position.Set("batteryAlarm", batteryAlarm);
                                    break;
                                case 0x00CE:
                                    position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 100.0);
                                    break;
                                case 0x00D8:
                                    network.AddCellTower(CellTower.From(
                                        buf.ReadUnsignedShort(), buf.ReadUnsignedByte(),
                                        buf.ReadUnsignedShort(), buf.ReadUnsignedInt()));
                                    break;
                                case 0x00A8:
                                case 0x00E1:
                                    position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                                    break;
                            }
                            buf.ReaderIndex = extendedEndIndex;
                        }
                    }
                    break;
                case 0xED:
                    stringValue = buf.ReadString(length, Encoding.Latin1);
                    position.Set(Position.KeyCard, JavaString.Trim(stringValue));
                    break;
                case 0xEE:
                    if (length == 6)
                    {
                        position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
                        position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 1000.0);
                        position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 1000.0);
                        position.Set(Position.KeySatellites, buf.ReadUnsignedByte());
                    }
                    break;
                case 0xF1:
                    position.Set(Position.KeyPower, buf.ReadUnsignedInt() / 1000.0);
                    break;
                case 0xF3:
                    while (buf.ReaderIndex < endIndex)
                    {
                        var extendedType = buf.ReadUnsignedShort();
                        var extendedLength = buf.ReadUnsignedByte();
                        switch (extendedType)
                        {
                            case 0x0002: position.Set(Position.KeyObdSpeed, buf.ReadUnsignedShort() / 10.0); break;
                            case 0x0003: position.Set(Position.KeyRpm, buf.ReadUnsignedShort()); break;
                            case 0x0004: position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 1000.0); break;
                            case 0x0005: position.Set(Position.KeyObdOdometer, buf.ReadUnsignedInt() * 100); break;
                            case 0x0007: position.Set(Position.KeyFuelConsumption, buf.ReadUnsignedShort() / 10.0); break;
                            case 0x0008: position.Set(Position.KeyEngineLoad, buf.ReadUnsignedShort() / 10.0); break;
                            case 0x0009: position.Set(Position.KeyCoolantTemp, buf.ReadUnsignedShort() - 40); break;
                            case 0x000B: position.Set("intakePressure", buf.ReadUnsignedShort()); break;
                            case 0x000C: position.Set("intakeTemp", buf.ReadUnsignedShort() - 40); break;
                            case 0x000D: position.Set("intakeFlow", buf.ReadUnsignedShort()); break;
                            case 0x000E: position.Set(Position.KeyThrottle, buf.ReadUnsignedShort() * 100 / 255); break;
                            case 0x0050: position.Set(Position.KeyVin, BufferUtil.ReadString(buf, 17)); break;
                            case 0x0051:
                                if (extendedLength > 0)
                                {
                                    position.Set("cvn", ByteBufferUtil.HexDump(buf.ReadSlice(extendedLength)));
                                }
                                break;
                            case 0x0052:
                                if (extendedLength > 0)
                                {
                                    position.Set("calid", BufferUtil.ReadString(buf, extendedLength));
                                }
                                break;
                            case 0x0100: position.Set(Position.KeyOdometerTrip, buf.ReadUnsignedShort() / 10.0); break;
                            case 0x0102: position.Set("tripFuel", buf.ReadUnsignedShort() / 10.0); break;
                            case 0x0112: position.Set("hardAccelerationCount", buf.ReadUnsignedShort()); break;
                            case 0x0113: position.Set("hardDecelerationCount", buf.ReadUnsignedShort()); break;
                            case 0x0114: position.Set("hardCorneringCount", buf.ReadUnsignedShort()); break;
                            default: buf.SkipBytes(extendedLength); break;
                        }
                    }
                    break;
                case 0xEC:
                case 0xF4:
                    while (buf.ReaderIndex < endIndex)
                    {
                        var mac = ByteBufferUtil.HexDump(buf.ReadSlice(6));
                        var macFormatted = string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                        network.AddWifiAccessPoint(WifiAccessPoint.From(macFormatted, buf.ReadByte()));
                    }
                    break;
                case 0xF5:
                    if (length == 2)
                    {
                        position.Set("illuminance", buf.ReadUnsignedShort());
                    }
                    break;
                case 0xF6:
                    if (length == 2)
                    {
                        position.Set("airPressure", buf.ReadUnsignedShort());
                    }
                    else if (length == 8)
                    {
                        position.Set("imei", ByteBufferUtil.HexDump(buf.ReadSlice(length)).Substring(1));
                    }
                    else
                    {
                        eventValue = buf.ReadUnsignedByte();
                        position.Set(Position.KeyEvent, eventValue);
                        if (eventValue == 2)
                        {
                            position.Set(Position.KeyMotion, true);
                        }
                        var fieldMask = buf.ReadUnsignedByte();
                        if (BitUtil.Check(fieldMask, 0))
                        {
                            position.Set("lightSensor", buf.ReadUnsignedShort());
                        }
                        if (BitUtil.Check(fieldMask, 1))
                        {
                            position.Set(Position.PrefixTemp + 1, buf.ReadShort() / 10.0);
                        }
                        if (BitUtil.Check(fieldMask, 2))
                        {
                            position.Set(Position.KeyHumidity, buf.ReadShort() / 10.0);
                        }
                    }
                    break;
                case 0xF7:
                    if (length == 2)
                    {
                        position.Set(Position.KeyHumidity, buf.ReadUnsignedShort() / 10.0);
                    }
                    else
                    {
                        position.Set(Position.KeyBattery, buf.ReadUnsignedInt() / 1000.0);
                        if (length >= 5)
                        {
                            var batteryStatus = buf.ReadUnsignedByte();
                            if (batteryStatus == 2 || batteryStatus == 3)
                            {
                                position.Set(Position.KeyCharge, true);
                            }
                        }
                        if (length >= 6)
                        {
                            position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                        }
                    }
                    break;
                case 0xF8:
                    if (model == "C5" || model == "C5L")
                    {
                        position.Set(Position.KeySteps, buf.ReadUnsignedShort());
                    }
                    else
                    {
                        position.Set(Position.PrefixTemp + 2, buf.ReadUnsignedShort() / 10.0 - 50);
                    }
                    break;
                case 0xFB:
                    position.Set("container", buf.ReadString(length, Encoding.Latin1));
                    break;
                case 0xFC:
                    position.Set(Position.KeyGeofence, buf.ReadUnsignedByte());
                    break;
                case 0xFE:
                    if (length == 1)
                    {
                        position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                    }
                    else if (length == 2)
                    {
                        position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 10.0);
                    }
                    else if (length == 4)
                    {
                        position.Set(Position.KeyOdometer, buf.ReadUnsignedInt());
                    }
                    else
                    {
                        var mark = buf.ReadUnsignedByte();
                        if (mark == 0x7C)
                        {
                            while (buf.ReaderIndex < endIndex)
                            {
                                var extendedType = buf.ReadUnsignedByte();
                                var extendedLength = buf.ReadUnsignedByte();
                                if (extendedType == 0x01)
                                {
                                    var alarms = buf.ReadUnsignedInt();
                                    if (BitUtil.Check(alarms, 0))
                                    {
                                        position.AddAlarm(Position.AlarmAcceleration);
                                    }
                                    if (BitUtil.Check(alarms, 1))
                                    {
                                        position.AddAlarm(Position.AlarmBraking);
                                    }
                                    if (BitUtil.Check(alarms, 2))
                                    {
                                        position.AddAlarm(Position.AlarmCornering);
                                    }
                                    if (BitUtil.Check(alarms, 3))
                                    {
                                        position.AddAlarm(Position.AlarmAccident);
                                    }
                                    if (BitUtil.Check(alarms, 4))
                                    {
                                        position.AddAlarm(Position.AlarmTampering);
                                    }
                                }
                                else
                                {
                                    buf.SkipBytes(extendedLength);
                                }
                            }
                        }
                        position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                    }
                    break;
            }
            buf.ReaderIndex = endIndex;
        }

        if (network.CellTowers != null || network.WifiAccessPoints != null)
        {
            position.Network = network;
        }
        return position;
    }

    // Inlined from Java's Jt600ProtocolDecoder, which JT808's "2" location messages reuse.
    private static double Jt600ConvertCoordinate(int raw)
    {
        var degrees = raw / 1000000;
        var minutes = (raw % 1000000) / 10000.0;
        return degrees + minutes / 60;
    }

    private static void Jt600DecodeBinaryLocation(ByteBuf buf, Position position)
    {
        var dateBuilder = new DateBuilder()
            .SetDay(BcdUtil.ReadInteger(buf, 2))
            .SetMonth(BcdUtil.ReadInteger(buf, 2))
            .SetYear(BcdUtil.ReadInteger(buf, 2))
            .SetHour(BcdUtil.ReadInteger(buf, 2))
            .SetMinute(BcdUtil.ReadInteger(buf, 2))
            .SetSecond(BcdUtil.ReadInteger(buf, 2));
        position.SetTime(dateBuilder.GetDate());

        var latitude = Jt600ConvertCoordinate(BcdUtil.ReadInteger(buf, 8));
        var longitude = Jt600ConvertCoordinate(BcdUtil.ReadInteger(buf, 9));

        var flags = buf.ReadByte();
        position.Valid = BitUtil.Check(flags, 0);
        position.Latitude = BitUtil.Check(flags, 1) ? latitude : -latitude;
        position.Longitude = BitUtil.Check(flags, 2) ? longitude : -longitude;

        position.Speed = BcdUtil.ReadInteger(buf, 2);
        position.Course = buf.ReadUnsignedByte() * 2.0;
    }

    private CellTower CellTowerFromCidLac(long cid, int lac)
    {
        var mcc = configuration.GetValue<int?>("Geolocation:Mcc") ?? 0;
        var mnc = configuration.GetValue<int?>("Geolocation:Mnc") ?? 0;
        return CellTower.From(mcc, mnc, lac, cid);
    }

    private Position DecodeLocation2(DeviceSession deviceSession, ByteBuf buf, int type)
    {
        var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

        Jt600DecodeBinaryLocation(buf, position);
        position.Valid = type != MsgLocationReportBlind;

        position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
        position.Set(Position.KeySatellites, buf.ReadUnsignedByte());
        position.Set(Position.KeyOdometer, buf.ReadUnsignedInt() * 1000L);

        var battery = buf.ReadUnsignedByte();
        if (battery <= 100)
        {
            position.Set(Position.KeyBatteryLevel, battery);
        }
        else if (battery == 0xAA || battery == 0xAB)
        {
            position.Set(Position.KeyCharge, true);
        }

        var cid = buf.ReadUnsignedInt();
        var lac = buf.ReadUnsignedShort();
        if (cid > 0 && lac > 0)
        {
            position.Network = new Network(CellTowerFromCidLac(cid, lac));
        }

        var product = buf.ReadUnsignedByte();
        var status = buf.ReadUnsignedShort();
        var alarm = buf.ReadUnsignedShort();

        if (product == 1 || product == 2)
        {
            if (BitUtil.Check(alarm, 0))
            {
                position.AddAlarm(Position.AlarmLowPower);
            }
        }
        else if (product == 3)
        {
            position.Set(Position.KeyBlocked, BitUtil.Check(status, 5));
            if (BitUtil.Check(alarm, 0))
            {
                position.AddAlarm(Position.AlarmOverspeed);
            }
            if (BitUtil.Check(alarm, 1))
            {
                position.AddAlarm(Position.AlarmLowPower);
            }
            if (BitUtil.Check(alarm, 2))
            {
                position.AddAlarm(Position.AlarmVibration);
            }
            if (BitUtil.Check(alarm, 3))
            {
                position.AddAlarm(Position.AlarmLowBattery);
            }
            if (BitUtil.Check(alarm, 5))
            {
                position.AddAlarm(Position.AlarmGeofenceEnter);
            }
            if (BitUtil.Check(alarm, 6))
            {
                position.AddAlarm(Position.AlarmGeofenceExit);
            }
        }

        position.Set(Position.KeyStatus, status);

        while (buf.ReadableBytes > 2)
        {
            var id = buf.ReadUnsignedByte();
            var length = buf.ReadUnsignedByte();
            switch (id)
            {
                case 0x02:
                    position.Altitude = buf.ReadShort();
                    break;
                case 0x10:
                    position.Set("wakeSource", buf.ReadUnsignedByte());
                    break;
                case 0x0A:
                    if (length == 3)
                    {
                        buf.ReadUnsignedShort(); // mcc
                        buf.ReadUnsignedByte(); // mnc
                    }
                    else
                    {
                        buf.SkipBytes(length);
                    }
                    break;
                case 0x0B:
                    position.Set("lockCommand", buf.ReadUnsignedByte());
                    if (length is >= 5 and <= 6)
                    {
                        position.Set("lockCard", buf.ReadUnsignedInt());
                    }
                    else if (length >= 7)
                    {
                        position.Set("lockPassword", buf.ReadString(6, Encoding.Latin1));
                    }
                    if (length % 2 == 0)
                    {
                        position.Set("unlockResult", buf.ReadUnsignedByte());
                    }
                    break;
                case 0x0C:
                    var x = buf.ReadUnsignedShort();
                    if (x > 0x8000)
                    {
                        x -= 0x10000;
                    }
                    var y = buf.ReadUnsignedShort();
                    if (y > 0x8000)
                    {
                        y -= 0x10000;
                    }
                    var z = buf.ReadUnsignedShort();
                    if (z > 0x8000)
                    {
                        z -= 0x10000;
                    }
                    position.Set("tilt", $"[{x},{y},{z}]");
                    break;
                case 0xFC:
                    position.Set(Position.KeyGeofence, buf.ReadUnsignedByte());
                    break;
                default:
                    buf.SkipBytes(length);
                    break;
            }
        }

        return position;
    }

    private List<Position> DecodeLocationBatch(DeviceSession deviceSession, ByteBuf buf, int type)
    {
        var positions = new List<Position>();

        var locationType = 0;
        if (type == MsgLocationBatch)
        {
            buf.ReadUnsignedShort(); // count
            locationType = buf.ReadUnsignedByte();
        }

        while (buf.ReadableBytes > 2)
        {
            var length = type == MsgLocationBatch2 ? buf.ReadUnsignedByte() : buf.ReadUnsignedShort();
            var fragment = new ByteBuf(buf.ReadSlice(length));
            var position = DecodeLocation(deviceSession, fragment);
            if (locationType > 0)
            {
                position.Set(Position.KeyArchive, true);
            }
            positions.Add(position);
        }

        return positions;
    }

    private Position? DecodeTransparent(DeviceSession deviceSession, ByteBuf buf)
    {
        var type = buf.ReadUnsignedByte();

        if (type == 0x40)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);
            var data = JavaString.Trim(buf.ReadString(buf.ReadableBytes, Encoding.Latin1));
            if (data.StartsWith("GTSL"))
            {
                var values = data.Split('|');
                if (values.Length > 4)
                {
                    position.Set(Position.KeyDriverUniqueId, values[4]);
                }
            }
            else
            {
                position.Set("data", data);
            }

            return position.Attributes.Count == 0 ? null : position;
        }
        else if (type == 0x41)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };
            GetLastLocation(position, null);

            var data = JavaString.Trim(buf.ReadString(buf.ReadableBytes - 2, Encoding.Latin1));
            DecodeObdRt(position, data);

            return position;
        }
        else if (type == 0xF0)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            var time = ReadDate(buf, deviceSession.Get<TimeZoneInfo>(DeviceSession.KeyTimezone));

            if (buf.ReadUnsignedByte() > 0)
            {
                position.Set(Position.KeyArchive, true);
            }

            buf.ReadUnsignedByte(); // vehicle type

            int count;
            var subtype = buf.ReadUnsignedByte();
            switch (subtype)
            {
                case 0x01:
                    count = buf.ReadUnsignedByte();
                    for (var i = 0; i < count; i++)
                    {
                        var id = buf.ReadUnsignedShort();
                        var length = buf.ReadUnsignedByte();
                        switch (id)
                        {
                            case 0x0102: case 0x0528: case 0x0546:
                                position.Set(Position.KeyOdometer, buf.ReadUnsignedInt() * 100);
                                break;
                            case 0x0103: position.Set(Position.KeyFuel, buf.ReadUnsignedInt() / 100.0); break;
                            case 0x0111: position.Set("fuelTemp", buf.ReadUnsignedByte() - 40); break;
                            case 0x012E: position.Set("oilLevel", buf.ReadUnsignedShort() / 10.0); break;
                            case 0x052A: position.Set(Position.KeyFuel, buf.ReadUnsignedShort() / 100.0); break;
                            case 0x0105: case 0x052C: position.Set(Position.KeyFuelUsed, buf.ReadUnsignedInt() / 100.0); break;
                            case 0x014A: case 0x0537: case 0x0538: case 0x0539:
                                position.Set(Position.KeyFuelConsumption, buf.ReadUnsignedShort() / 100.0);
                                break;
                            case 0x052B: position.Set(Position.KeyFuel, buf.ReadUnsignedByte()); break;
                            case 0x052D: position.Set(Position.KeyCoolantTemp, buf.ReadUnsignedByte() - 40); break;
                            case 0x052E: position.Set("airTemp", buf.ReadUnsignedByte() - 40); break;
                            case 0x0530: position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 1000.0); break;
                            case 0x0535: position.Set(Position.KeyObdSpeed, buf.ReadUnsignedShort() / 10.0); break;
                            case 0x0536: position.Set(Position.KeyRpm, buf.ReadUnsignedShort()); break;
                            case 0x053D: position.Set("intakePressure", buf.ReadUnsignedShort() / 10.0); break;
                            case 0x0544: position.Set("liquidLevel", buf.ReadUnsignedByte()); break;
                            case 0x0547: case 0x0548: position.Set(Position.KeyThrottle, buf.ReadUnsignedByte()); break;
                            default:
                                switch (length)
                                {
                                    case 1: position.Set(Position.PrefixIo + id, buf.ReadUnsignedByte()); break;
                                    case 2: position.Set(Position.PrefixIo + id, buf.ReadUnsignedShort()); break;
                                    case 4: position.Set(Position.PrefixIo + id, buf.ReadUnsignedInt()); break;
                                    default: buf.SkipBytes(length); break;
                                }
                                break;
                        }
                    }
                    GetLastLocation(position, time);
                    DecodeCoordinates(position, deviceSession, buf);
                    position.SetTime(time);
                    break;
                case 0x02:
                    var codes = new List<string>();
                    count = buf.ReadUnsignedShort();
                    for (var i = 0; i < count; i++)
                    {
                        buf.ReadUnsignedInt(); // system id
                        var codeCount = buf.ReadUnsignedShort();
                        for (var j = 0; j < codeCount; j++)
                        {
                            buf.ReadUnsignedInt(); // dtc
                            buf.ReadUnsignedInt(); // status
                            codes.Add(JavaString.Trim(buf.ReadString(buf.ReadUnsignedShort(), Encoding.Latin1)));
                        }
                    }
                    position.Set(Position.KeyDtcs, string.Join(" ", codes));
                    GetLastLocation(position, time);
                    DecodeCoordinates(position, deviceSession, buf);
                    position.SetTime(time);
                    break;
                case 0x03:
                    count = buf.ReadUnsignedByte();
                    for (var i = 0; i < count; i++)
                    {
                        var id = buf.ReadUnsignedByte();
                        var length = buf.ReadUnsignedByte();
                        switch (id)
                        {
                            case 0x01: position.AddAlarm(Position.AlarmPowerRestored); break;
                            case 0x02: position.AddAlarm(Position.AlarmPowerCut); break;
                            case 0x1A: position.AddAlarm(Position.AlarmAcceleration); break;
                            case 0x1B: position.AddAlarm(Position.AlarmBraking); break;
                            case 0x1C: position.AddAlarm(Position.AlarmCornering); break;
                            case 0x1D: case 0x1E: case 0x1F: position.AddAlarm(Position.AlarmLaneChange); break;
                            case 0x23: position.AddAlarm(Position.AlarmFatigueDriving); break;
                            case 0x26: case 0x27: case 0x28: position.AddAlarm(Position.AlarmAccident); break;
                            case 0x31: case 0x32: position.AddAlarm(Position.AlarmDoor); break;
                        }
                        buf.SkipBytes(length);
                    }
                    GetLastLocation(position, time);
                    DecodeCoordinates(position, deviceSession, buf);
                    position.SetTime(time);
                    break;
                case 0x0B:
                    if (buf.ReadUnsignedByte() > 0)
                    {
                        position.Set(Position.KeyVin, buf.ReadString(17, Encoding.Latin1));
                    }
                    GetLastLocation(position, time);
                    break;
                case 0x15:
                    var eventValue = buf.ReadInt();
                    switch (eventValue)
                    {
                        case 51: position.AddAlarm(Position.AlarmAcceleration); break;
                        case 52: position.AddAlarm(Position.AlarmBraking); break;
                        case 53: position.AddAlarm(Position.AlarmCornering); break;
                        case 54: position.AddAlarm(Position.AlarmLaneChange); break;
                        case 56: position.AddAlarm(Position.AlarmAccident); break;
                        default: position.Set(Position.KeyEvent, eventValue); break;
                    }
                    GetLastLocation(position, time);
                    break;
                default:
                    return null;
            }

            return position;
        }
        else if (type == 0xFF)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            position.Valid = true;
            position.SetTime(ReadDate(buf, deviceSession.Get<TimeZoneInfo>(DeviceSession.KeyTimezone)));
            position.Latitude = buf.ReadInt() / 1000000.0;
            position.Longitude = buf.ReadInt() / 1000000.0;
            position.Altitude = buf.ReadShort();
            position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedShort() / 10.0);
            position.Course = buf.ReadUnsignedShort();

            // TODO more positions and g sensor data

            return position;
        }
        else if (type == 0xF3)
        {
            var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

            while (buf.ReadableBytes > 4)
            {
                var subtype = buf.ReadUnsignedShort();
                var length = buf.ReadUnsignedShort();
                var endIndex = buf.ReaderIndex + length;
                switch (subtype)
                {
                    case 0x0002: position.Set("collision", buf.ReadUnsignedShort() / 256.0 / 100.0); break;
                    case 0x0006: position.DeviceTime = ReadDate(buf, deviceSession.Get<TimeZoneInfo>(DeviceSession.KeyTimezone)); break;
                }
                buf.ReaderIndex = endIndex;
            }

            GetLastLocation(position, position.DeviceTime);

            return position;
        }

        return null;
    }
}
