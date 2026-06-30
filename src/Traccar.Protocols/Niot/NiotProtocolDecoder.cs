using System.Net;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Niot;

public sealed class NiotProtocolDecoder(ConnectionManager connectionManager, ILogger<NiotProtocolDecoder> logger)
    : BaseProtocolDecoder("niot", connectionManager, logger)
{
    public const int MsgResponse = 0x21;
    public const int MsgPositionData = 0x80;

    private void SendResponse(IChannel channel, EndPoint? remoteAddress, int type, int checksum)
    {
        var response = Unpooled.Buffer();
        response.WriteShort(0x5858); // header
        response.WriteByte(MsgResponse);
        response.WriteShort(5); // length
        response.WriteByte(checksum);
        response.WriteByte(type);
        response.WriteByte(0); // subtype

        var checksumBytes = new byte[response.ReadableBytes - 2];
        response.GetBytes(2, checksumBytes);
        response.WriteByte(Checksum.Xor(checksumBytes));
        response.WriteByte(0x0D);
        WriteResponse(channel, remoteAddress, response);
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = (IByteBuffer)message;
        var wrapped = new ByteBuf(buf);

        wrapped.SkipBytes(2); // header
        var type = wrapped.ReadUnsignedByte();
        wrapped.ReadUnsignedShort(); // length

        var imei = ByteBufferUtil.HexDump(wrapped.ReadSlice(8))[1..];

        SendResponse(channel, remoteAddress, type, wrapped.GetByte(wrapped.WriterIndex - 2));

        if (type == MsgPositionData)
        {
            var position = new Position(ProtocolName);

            var deviceSession = GetDeviceSession(channel, remoteAddress, imei);
            if (deviceSession == null)
            {
                return null;
            }
            position.DeviceId = deviceSession.DeviceId;

            var dateBuilder = new DateBuilder()
                .SetYear(BcdUtil.ReadInteger(wrapped, 2))
                .SetMonth(BcdUtil.ReadInteger(wrapped, 2))
                .SetDay(BcdUtil.ReadInteger(wrapped, 2))
                .SetHour(BcdUtil.ReadInteger(wrapped, 2))
                .SetMinute(BcdUtil.ReadInteger(wrapped, 2))
                .SetSecond(BcdUtil.ReadInteger(wrapped, 2));
            position.SetTime(dateBuilder.GetDate());

            position.Latitude = BufferUtil.ReadSignedMagnitudeInt(wrapped) / 1800000.0;
            position.Longitude = BufferUtil.ReadSignedMagnitudeInt(wrapped) / 1800000.0;
            BcdUtil.ReadInteger(wrapped, 4); // reserved
            position.Course = BcdUtil.ReadInteger(wrapped, 4);

            var statusX = wrapped.ReadUnsignedByte();
            position.Valid = BitUtil.Check(statusX, 7);
            switch (BitUtil.Between(statusX, 3, 5))
            {
                case 0b10: position.AddAlarm(Position.AlarmPowerCut); break;
                case 0b01: position.AddAlarm(Position.AlarmLowPower); break;
            }

            position.Set(Position.KeyOdometer, wrapped.ReadUnsignedInt());

            var statusA = wrapped.ReadUnsignedByte();
            position.Set(Position.KeyIgnition, !BitUtil.Check(statusA, 7));
            if (!BitUtil.Check(statusA, 6))
            {
                position.AddAlarm(Position.AlarmOverspeed);
            }

            wrapped.ReadUnsignedByte(); // statusB
            wrapped.ReadUnsignedByte(); // statusC
            position.Set(Position.KeySatellites, wrapped.ReadUnsignedByte());
            position.Set(Position.KeyBattery, wrapped.ReadUnsignedByte() / 10.0);
            position.Set(Position.KeyPower, wrapped.ReadUnsignedShort() / 10.0);
            wrapped.ReadUnsignedByte(); // speed limit
            position.Speed = UnitsConverter.KnotsFromKph(wrapped.ReadUnsignedByte());
            wrapped.ReadUnsignedByte(); // sensor speed
            wrapped.ReadUnsignedByte(); // reserved
            wrapped.ReadUnsignedByte(); // reserved

            while (wrapped.ReadableBytes > 4)
            {
                var extendedLength = wrapped.ReadUnsignedShort();
                var extendedType = wrapped.ReadUnsignedShort();
                switch (extendedType)
                {
                    case 0x0001:
                        position.Set(Position.KeyIccid, wrapped.ReadString(20, Encoding.Latin1));
                        break;
                    case 0x0002:
                        var statusD = wrapped.ReadUnsignedByte();
                        position.AddAlarm(BitUtil.Check(statusD, 5) ? Position.AlarmRemoving : null);
                        position.AddAlarm(BitUtil.Check(statusD, 4) ? Position.AlarmTampering : null);
                        wrapped.ReadUnsignedByte(); // run mode
                        wrapped.ReadUnsignedByte(); // reserved
                        break;
                    default:
                        wrapped.SkipBytes(extendedLength - 2);
                        break;
                }
            }

            return position;
        }

        return null;
    }
}
