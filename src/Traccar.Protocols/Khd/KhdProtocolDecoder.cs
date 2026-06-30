using System.Net;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Khd;

public sealed class KhdProtocolDecoder(ConnectionManager connectionManager, ILogger<KhdProtocolDecoder> logger)
    : BaseProtocolDecoder("khd", connectionManager, logger)
{
    public const int MsgLogin = 0xB1;
    public const int MsgConfirmation = 0x21;
    public const int MsgOnDemand = 0x81;
    public const int MsgPositionUpload = 0x80;
    public const int MsgPositionReupload = 0x8E;
    public const int MsgAlarm = 0x82;
    public const int MsgAdminNumber = 0x83;
    public const int MsgSendText = 0x84;
    public const int MsgReply = 0x85;
    public const int MsgSmsAlarmSwitch = 0x86;
    public const int MsgPeripheral = 0xA3;

    private static string[] ReadIdentifiers(ByteBuf buf)
    {
        var identifiers = new string[2];

        identifiers[0] = ByteBufferUtil.HexDump(buf.Inner, buf.ReaderIndex, 4);

        var b1 = buf.ReadUnsignedByte();
        var b2 = buf.ReadUnsignedByte() - 0x80;
        var b3 = buf.ReadUnsignedByte() - 0x80;
        var b4 = buf.ReadUnsignedByte();
        identifiers[1] = $"{b1:D2}{b2:D2}{b3:D2}{b4:D2}";

        return identifiers;
    }

    private static void DecodeAlarmStatus(Position position, byte[] status)
    {
        if (BitUtil.Check(status[0], 4))
        {
            position.AddAlarm(Position.AlarmLowPower);
        }
        else if (BitUtil.Check(status[0], 6))
        {
            position.AddAlarm(Position.AlarmGeofenceExit);
        }
        else if (BitUtil.Check(status[0], 7))
        {
            position.AddAlarm(Position.AlarmGeofenceEnter);
        }
        else if (BitUtil.Check(status[1], 0))
        {
            position.AddAlarm(Position.AlarmSos);
        }
        else if (BitUtil.Check(status[1], 1))
        {
            position.AddAlarm(Position.AlarmOverspeed);
        }
        else if (BitUtil.Check(status[1], 3))
        {
            position.AddAlarm(Position.AlarmPowerCut);
        }
        else if (BitUtil.Check(status[1], 6))
        {
            position.AddAlarm(Position.AlarmTow);
        }
        else if (BitUtil.Check(status[1], 7))
        {
            position.AddAlarm(Position.AlarmDoor);
        }
        else if (BitUtil.Check(status[2], 2))
        {
            position.AddAlarm(Position.AlarmTemperature);
        }
        else if (BitUtil.Check(status[2], 4))
        {
            position.AddAlarm(Position.AlarmTampering);
        }
        else if (BitUtil.Check(status[2], 6))
        {
            position.AddAlarm(Position.AlarmFatigueDriving);
        }
        else if (BitUtil.Check(status[2], 7))
        {
            position.AddAlarm(Position.AlarmIdle);
        }
        else if (BitUtil.Check(status[6], 3))
        {
            position.AddAlarm(Position.AlarmVibration);
        }
        else if (BitUtil.Check(status[6], 4))
        {
            position.AddAlarm(Position.AlarmBraking);
        }
        else if (BitUtil.Check(status[6], 5))
        {
            position.AddAlarm(Position.AlarmAcceleration);
        }
        else if (BitUtil.Check(status[6], 6))
        {
            position.AddAlarm(Position.AlarmCornering);
        }
        else if (BitUtil.Check(status[6], 7))
        {
            position.AddAlarm(Position.AlarmAccident);
        }
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = new ByteBuf((IByteBuffer)message);

        buf.SkipBytes(2); // header
        var type = buf.ReadUnsignedByte();
        buf.ReadUnsignedShort(); // size

        if (type is MsgLogin or MsgAdminNumber or MsgSendText or MsgSmsAlarmSwitch or MsgPositionReupload)
        {
            var response = Unpooled.Buffer();
            response.WriteByte(0x29);
            response.WriteByte(0x29); // header
            response.WriteByte(MsgConfirmation);
            response.WriteShort(5); // size
            response.WriteByte(buf.GetByte(buf.WriterIndex - 2));
            response.WriteByte(type);
            response.WriteByte(buf.WriterIndex > 9 ? buf.GetByte(9) : 0); // 10th byte

            var crcBytes = new byte[response.ReadableBytes];
            response.GetBytes(response.ReaderIndex, crcBytes);
            response.WriteByte(Checksum.Xor(crcBytes));
            response.WriteByte(0x0D); // ending

            channel.WriteAndFlushAsync(response);
        }

        if (type is MsgOnDemand or MsgPositionUpload or MsgPositionReupload or MsgAlarm or MsgReply or MsgPeripheral)
        {
            var position = new Position(ProtocolName);

            var deviceSession = GetDeviceSession(channel, remoteAddress, ReadIdentifiers(buf));
            if (deviceSession == null)
            {
                return null;
            }
            position.DeviceId = deviceSession.DeviceId;

            var dateBuilder = new DateBuilder()
                .SetYear(BcdUtil.ReadInteger(buf, 2))
                .SetMonth(BcdUtil.ReadInteger(buf, 2))
                .SetDay(BcdUtil.ReadInteger(buf, 2))
                .SetHour(BcdUtil.ReadInteger(buf, 2))
                .SetMinute(BcdUtil.ReadInteger(buf, 2))
                .SetSecond(BcdUtil.ReadInteger(buf, 2));
            position.SetTime(dateBuilder.GetDate());

            position.Latitude = BcdUtil.ReadCoordinate(buf);
            position.Longitude = BcdUtil.ReadCoordinate(buf);
            position.Speed = UnitsConverter.KnotsFromKph(BcdUtil.ReadInteger(buf, 4));
            position.Course = BcdUtil.ReadInteger(buf, 4);
            position.Valid = (buf.ReadUnsignedByte() & 0x80) != 0;

            if (type != MsgAlarm)
            {
                var odometer = buf.ReadUnsignedMedium();
                if (BitUtil.To(odometer, 16) > 0)
                {
                    position.Set(Position.KeyOdometer, odometer);
                }
                else if (odometer > 0)
                {
                    position.Set(Position.KeyFuel, BitUtil.From(odometer, 16));
                }

                var status = buf.ReadUnsignedInt();
                position.Set(Position.KeyIgnition, !BitUtil.Check(status, 7 + 3 * 8));
                position.Set(Position.KeyStatus, status);

                buf.ReadUnsignedShort();
                buf.ReadByte();
                buf.ReadByte();
                buf.ReadByte();
                buf.ReadByte();
                buf.ReadByte();

                position.Set(Position.KeyResult, buf.ReadUnsignedByte().ToString());

                if (type == MsgPeripheral)
                {
                    buf.ReadUnsignedShort(); // data length

                    var dataType = buf.ReadUnsignedByte();
                    var dataLength = buf.ReadUnsignedByte();

                    switch (dataType)
                    {
                        case 0x01:
                            position.Set(Position.KeyFuel, buf.ReadUnsignedByte() * 100 + buf.ReadUnsignedByte());
                            break;
                        case 0x02:
                            position.Set(Position.PrefixTemp + 1, buf.ReadUnsignedByte() * 100 + buf.ReadUnsignedByte());
                            break;
                        case 0x05:
                            var sign = buf.ReadUnsignedByte();
                            switch (sign)
                            {
                                case 1:
                                    position.Set("sign", true);
                                    break;
                                case 2:
                                    position.Set("sign", false);
                                    break;
                            }
                            position.Set(Position.KeyDriverUniqueId, BufferUtil.ReadString(buf, dataLength - 1));
                            break;
                        case 0x18:
                            for (var i = 1; i <= 4; i++)
                            {
                                double value = buf.ReadUnsignedShort();
                                if (value is > 0x0000 and < 0xFFFF)
                                {
                                    position.Set("fuel" + i, value / 0xFFFE);
                                }
                            }
                            break;
                        case 0x20:
                            position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
                            break;
                        case 0x23:
                            var network = new Network();
                            var count = buf.ReadUnsignedByte();
                            for (var i = 0; i < count; i++)
                            {
                                network.AddCellTower(CellTower.From(
                                    buf.ReadUnsignedShort(), buf.ReadUnsignedByte(),
                                    buf.ReadUnsignedShort(), buf.ReadUnsignedShort(), buf.ReadUnsignedByte()));
                            }
                            if (count > 0)
                            {
                                position.Network = network;
                            }
                            break;
                    }
                }
            }
            else
            {
                buf.ReadUnsignedByte(); // overloaded state
                buf.ReadUnsignedByte(); // logging status

                var alarmStatus = new byte[8];
                buf.ReadBytes(alarmStatus);

                DecodeAlarmStatus(position, alarmStatus);
            }

            return position;
        }

        return null;
    }
}
