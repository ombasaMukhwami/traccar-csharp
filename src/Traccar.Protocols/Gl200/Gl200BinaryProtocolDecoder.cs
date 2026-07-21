using System.Net;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Gl200;

public sealed class Gl200BinaryProtocolDecoder(ConnectionManager connectionManager, ILogger<Gl200BinaryProtocolDecoder> logger)
    : BaseProtocolDecoder("gl200", connectionManager, logger)
{
    public const int MsgRspLcb = 3;
    public const int MsgRspGeo = 8;
    public const int MsgRspCompressed = 100;

    public const int MsgEvtBpl = 6;
    public const int MsgEvtVgn = 45;
    public const int MsgEvtVgf = 46;
    public const int MsgEvtUpd = 15;
    public const int MsgEvtIdf = 17;
    public const int MsgEvtGss = 21;
    public const int MsgEvtGes = 26;
    public const int MsgEvtGpj = 31;
    public const int MsgEvtRmd = 35;
    public const int MsgEvtJds = 33;
    public const int MsgEvtCra = 23;
    public const int MsgEvtUpc = 34;

    public const int MsgInfGps = 2;
    public const int MsgInfCid = 4;
    public const int MsgInfCsq = 5;
    public const int MsgInfVer = 6;
    public const int MsgInfBat = 7;
    public const int MsgInfTmz = 9;
    public const int MsgInfGir = 10;

    private static DateTime DecodeTime(ByteBuf buf)
    {
        var dateBuilder = new DateBuilder()
            .SetDate(buf.ReadUnsignedShort(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte())
            .SetTime(buf.ReadUnsignedByte(), buf.ReadUnsignedByte(), buf.ReadUnsignedByte());
        return dateBuilder.GetDate();
    }

    private List<Position>? DecodeLocation(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var positions = new List<Position>();

        var type = buf.ReadUnsignedByte();

        buf.ReadUnsignedInt(); // mask
        buf.ReadUnsignedShort(); // length
        buf.ReadUnsignedByte(); // device type
        buf.ReadUnsignedShort(); // protocol version
        buf.ReadUnsignedShort(); // firmware version

        var deviceSession = GetDeviceSession(channel, remoteAddress, buf.ReadLong().ToString("D15"));
        if (deviceSession == null)
        {
            return null;
        }

        var battery = buf.ReadUnsignedByte();
        var power = buf.ReadUnsignedShort();

        if (type == MsgRspGeo)
        {
            buf.ReadUnsignedByte(); // reserved
            buf.ReadUnsignedByte(); // reserved
        }

        buf.ReadUnsignedByte(); // motion status
        var satellites = buf.ReadUnsignedByte();

        if (type != MsgRspCompressed)
        {
            buf.ReadUnsignedByte(); // index
        }

        if (type == MsgRspLcb)
        {
            buf.ReadUnsignedByte(); // phone length
            for (var b = buf.ReadUnsignedByte(); ; b = buf.ReadUnsignedByte())
            {
                if ((b & 0xf) == 0xf || (b & 0xf0) == 0xf0)
                {
                    break;
                }
            }
        }

        if (type == MsgRspCompressed)
        {
            var count = buf.ReadUnsignedShort();

            var speed = 0;
            var heading = 0;
            var latitude = 0;
            var longitude = 0;
            long time = 0;

            for (var i = 0; i < count; i++)
            {
                if (time > 0)
                {
                    time += 1;
                }

                var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

                BitBuffer bits;
                switch (BitUtil.From(buf.GetUnsignedByte(buf.ReaderIndex), 8 - 2))
                {
                    case 1:
                        bits = new BitBuffer(buf.ReadSlice(3));
                        bits.ReadUnsigned(2); // point attribute
                        bits.ReadUnsigned(1); // fix type
                        speed = bits.ReadUnsigned(12);
                        heading = bits.ReadUnsigned(9);
                        longitude = buf.ReadInt();
                        latitude = buf.ReadInt();
                        if (time == 0)
                        {
                            time = buf.ReadUnsignedInt();
                        }
                        break;
                    case 2:
                        bits = new BitBuffer(buf.ReadSlice(5));
                        bits.ReadUnsigned(2); // point attribute
                        bits.ReadUnsigned(1); // fix type
                        speed += bits.ReadSigned(7);
                        heading += bits.ReadSigned(7);
                        longitude += bits.ReadSigned(12);
                        latitude += bits.ReadSigned(11);
                        break;
                    default:
                        buf.ReadUnsignedByte(); // invalid or same
                        continue;
                }

                position.Valid = true;
                position.SetTime(DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime);
                position.Speed = UnitsConverter.KnotsFromKph(speed / 10.0);
                position.Course = heading;
                position.Longitude = longitude / 1000000.0;
                position.Latitude = latitude / 1000000.0;

                positions.Add(position);
            }
        }
        else
        {
            var count = buf.ReadUnsignedByte();

            for (var i = 0; i < count; i++)
            {
                var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

                position.Set(Position.KeyBatteryLevel, battery);
                position.Set(Position.KeyPower, power);
                position.Set(Position.KeySatellites, satellites);

                var hdop = buf.ReadUnsignedByte();
                position.Valid = hdop > 0;
                position.Set(Position.KeyHdop, hdop);

                position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedMedium() / 10.0);
                position.Course = buf.ReadUnsignedShort();
                position.Altitude = buf.ReadShort();
                position.Longitude = buf.ReadInt() / 1000000.0;
                position.Latitude = buf.ReadInt() / 1000000.0;

                position.SetTime(DecodeTime(buf));

                var network = new Network();
                network.AddCellTower(CellTower.From(
                    buf.ReadUnsignedShort(), buf.ReadUnsignedShort(),
                    buf.ReadUnsignedShort(), buf.ReadUnsignedShort()));
                position.Network = network;

                buf.ReadUnsignedByte(); // reserved

                positions.Add(position);
            }
        }

        return positions;
    }

    private Position? DecodeEvent(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var position = new Position(ProtocolName);

        var type = buf.ReadUnsignedByte();

        buf.ReadUnsignedInt(); // mask
        buf.ReadUnsignedShort(); // length
        buf.ReadUnsignedByte(); // device type
        buf.ReadUnsignedShort(); // protocol version

        position.Set(Position.KeyVersionFw, buf.ReadUnsignedShort().ToString());

        var deviceSession = GetDeviceSession(channel, remoteAddress, buf.ReadLong().ToString("D15"));
        if (deviceSession == null)
        {
            return null;
        }
        position.DeviceId = deviceSession.DeviceId;

        position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
        position.Set(Position.KeyPower, buf.ReadUnsignedShort());

        buf.ReadUnsignedByte(); // motion status

        position.Set(Position.KeySatellites, buf.ReadUnsignedByte());

        switch (type)
        {
            case MsgEvtBpl: buf.ReadUnsignedShort(); break; // backup battery voltage
            case MsgEvtVgn or MsgEvtVgf:
                buf.ReadUnsignedShort(); // reserved
                buf.ReadUnsignedByte(); // report type
                buf.ReadUnsignedInt(); // ignition duration
                break;
            case MsgEvtUpd:
                buf.ReadUnsignedShort(); // code
                buf.ReadUnsignedByte(); // retry
                break;
            case MsgEvtIdf: buf.ReadUnsignedInt(); break; // idling duration
            case MsgEvtGss:
                buf.ReadUnsignedByte(); // gps signal status
                buf.ReadUnsignedInt(); // reserved
                break;
            case MsgEvtGes:
                buf.ReadUnsignedShort(); // trigger geo id
                buf.ReadUnsignedByte(); // trigger geo enable
                buf.ReadUnsignedByte(); // trigger mode
                buf.ReadUnsignedInt(); // radius
                buf.ReadUnsignedInt(); // check interval
                break;
            case MsgEvtGpj:
                buf.ReadUnsignedByte(); // cw jamming value
                buf.ReadUnsignedByte(); // gps jamming state
                break;
            case MsgEvtRmd: buf.ReadUnsignedByte(); break; // roaming state
            case MsgEvtJds: buf.ReadUnsignedByte(); break; // jamming state
            case MsgEvtCra: buf.ReadUnsignedByte(); break; // crash counter
            case MsgEvtUpc:
                buf.ReadUnsignedByte(); // command id
                buf.ReadUnsignedShort(); // result
                break;
        }

        buf.ReadUnsignedByte(); // count

        var hdop = buf.ReadUnsignedByte();
        position.Valid = hdop > 0;
        position.Set(Position.KeyHdop, hdop);

        position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedMedium() / 10.0);
        position.Course = buf.ReadUnsignedShort();
        position.Altitude = buf.ReadShort();
        position.Longitude = buf.ReadInt() / 1000000.0;
        position.Latitude = buf.ReadInt() / 1000000.0;

        position.SetTime(DecodeTime(buf));

        var network = new Network();
        network.AddCellTower(CellTower.From(
            buf.ReadUnsignedShort(), buf.ReadUnsignedShort(),
            buf.ReadUnsignedShort(), buf.ReadUnsignedShort()));
        position.Network = network;

        buf.ReadUnsignedByte(); // reserved

        return position;
    }

    private Position? DecodeInformation(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var position = new Position(ProtocolName);

        var type = buf.ReadUnsignedByte();

        buf.ReadUnsignedInt(); // mask
        buf.ReadUnsignedShort(); // length

        var deviceSession = GetDeviceSession(channel, remoteAddress, buf.ReadLong().ToString("D15"));
        if (deviceSession == null)
        {
            return null;
        }
        position.DeviceId = deviceSession.DeviceId;

        buf.ReadUnsignedByte(); // device type
        buf.ReadUnsignedShort(); // protocol version

        position.Set(Position.KeyVersionFw, buf.ReadUnsignedShort().ToString());

        if (type == MsgInfVer)
        {
            buf.ReadUnsignedShort(); // hardware version
            buf.ReadUnsignedShort(); // mcu version
            buf.ReadUnsignedShort(); // reserved
        }

        buf.ReadUnsignedByte(); // motion status
        buf.ReadUnsignedByte(); // reserved

        position.Set(Position.KeySatellites, buf.ReadUnsignedByte());

        buf.ReadUnsignedByte(); // mode
        buf.SkipBytes(7); // last fix time
        buf.ReadUnsignedByte(); // reserved
        buf.ReadUnsignedByte();
        buf.ReadUnsignedShort(); // response report mask
        buf.ReadUnsignedShort(); // ign interval
        buf.ReadUnsignedShort(); // igf interval
        buf.ReadUnsignedInt(); // reserved
        buf.ReadUnsignedByte(); // reserved

        if (type == MsgInfBat)
        {
            position.Set(Position.KeyCharge, buf.ReadUnsignedByte() != 0);
            position.Set(Position.KeyPower, buf.ReadUnsignedShort() / 1000.0);
            position.Set(Position.KeyBattery, buf.ReadUnsignedShort() / 1000.0);
            position.Set(Position.KeyBatteryLevel, buf.ReadUnsignedByte());
        }

        buf.SkipBytes(10); // iccid

        if (type == MsgInfCsq)
        {
            position.Set(Position.KeyRssi, buf.ReadUnsignedByte());
            buf.ReadUnsignedByte();
        }

        buf.ReadUnsignedByte(); // time zone flags
        buf.ReadUnsignedShort(); // time zone offset

        if (type == MsgInfGir)
        {
            buf.ReadUnsignedByte(); // gir trigger
            buf.ReadUnsignedByte(); // cell number
            var network = new Network();
            network.AddCellTower(CellTower.From(
                buf.ReadUnsignedShort(), buf.ReadUnsignedShort(),
                buf.ReadUnsignedShort(), buf.ReadUnsignedShort()));
            position.Network = network;
            buf.ReadUnsignedByte(); // ta
            buf.ReadUnsignedByte(); // rx level
        }

        GetLastLocation(position, DecodeTime(buf));

        return position;
    }

    private static int ReadVariableLength(ByteBuf buf)
    {
        var value = buf.ReadUnsignedByte();
        if (BitUtil.Check(value, 7))
        {
            value = BitUtil.To(value, 7) << 8 | buf.ReadUnsignedByte();
        }
        return value;
    }

    private List<Position>? DecodeBinary(IChannel channel, EndPoint? remoteAddress, ByteBuf buf)
    {
        var frameStart = buf.ReaderIndex;

        buf.ReadUnsignedByte(); // header
        buf.ReadUnsignedByte(); // identifier
        var frameLength = buf.ReadUnsignedShort();

        if (BitUtil.Check(buf.ReadUnsignedByte(), 7)) // multi-packet flag
        {
            buf.ReadUnsignedShort(); // frame count and frame number
        }

        var imei = ByteBufferUtil.HexDump(buf.ReadSlice(8))[1..];
        var deviceSession = GetDeviceSession(channel, remoteAddress, imei);
        if (deviceSession == null)
        {
            return null;
        }

        buf.ReadUnsignedShort(); // device type
        buf.ReadUnsignedShort(); // protocol version
        buf.ReadUnsignedByte(); // custom version

        buf.SkipBytes(buf.ReadUnsignedByte()); // reserved field

        var positions = new List<Position>();

        var recordsEnd = frameStart + frameLength - 4; // count number, check byte and tail

        while (buf.ReaderIndex < recordsEnd)
        {
            var recordStart = buf.ReaderIndex;
            var recordEnd = recordStart + ReadVariableLength(buf);

            buf.ReadUnsignedInt(); // generated time
            buf.ReadUnsignedShort(); // record count number
            buf.ReadUnsignedByte(); // record id
            buf.ReadUnsignedByte(); // event code

            while (buf.ReaderIndex < recordEnd)
            {
                var dataId = ReadVariableLength(buf);
                var dataEnd = buf.ReaderIndex + ReadVariableLength(buf);

                if (dataId == 0x52)
                {
                    var position = new Position(ProtocolName) { DeviceId = deviceSession.DeviceId };

                    position.Valid = BitUtil.Between(buf.ReadUnsignedByte(), 2, 4) == 0b10;
                    position.Longitude = buf.ReadInt() / 1000000.0;
                    position.Latitude = buf.ReadInt() / 1000000.0;
                    position.SetTime(DateTimeOffset.FromUnixTimeSeconds(buf.ReadUnsignedInt()).UtcDateTime);
                    position.Speed = UnitsConverter.KnotsFromKph(buf.ReadUnsignedShort() / 10.0);

                    position.Set(Position.KeyHdop, buf.ReadUnsignedByte() / 10.0);
                    position.Course = buf.ReadUnsignedShort();
                    position.Altitude = buf.ReadUnsignedMedium() / 10.0;
                    position.Set(Position.KeySatellites, buf.ReadUnsignedByte());

                    positions.Add(position);
                }

                buf.ReaderIndex = dataEnd;
            }

            buf.ReaderIndex = recordEnd;
        }

        return positions;
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = new ByteBuf((IByteBuffer)message);

        if (buf.GetUnsignedByte(buf.ReaderIndex + 1) == 0)
        {
            return DecodeBinary(channel, remoteAddress, buf);
        }

        return buf.ReadString(4, System.Text.Encoding.ASCII) switch
        {
            "+RSP" => DecodeLocation(channel, remoteAddress, buf),
            "+INF" => DecodeInformation(channel, remoteAddress, buf),
            "+EVT" => DecodeEvent(channel, remoteAddress, buf),
            _ => null,
        };
    }
}
