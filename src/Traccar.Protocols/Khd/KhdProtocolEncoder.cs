using DotNetty.Buffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Protocols.Khd;

public sealed class KhdProtocolEncoder(IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<KhdProtocolEncoder> logger)
    : BaseProtocolEncoder("khd", dbContextFactory, logger)
{
    public const int MsgOnDemandTrack = 0x30;
    public const int MsgCutOil = 0x39;
    public const int MsgResumeOil = 0x38;
    public const int MsgCheckVersion = 0x3D;
    public const int MsgFactoryReset = 0xC3;
    public const int MsgSetOverspeed = 0x3F;
    public const int MsgDeleteMileage = 0x66;

    private static IByteBuffer EncodeCommand(int command, string uniqueId, IByteBuffer? content)
    {
        var buf = Unpooled.Buffer();

        buf.WriteByte(0x29);
        buf.WriteByte(0x29);

        buf.WriteByte(command);

        var length = 6 + (content?.ReadableBytes ?? 0);
        buf.WriteShort(length);

        uniqueId = ("00000000" + uniqueId);
        uniqueId = uniqueId[^8..];
        buf.WriteByte(int.Parse(uniqueId[..2]));
        buf.WriteByte(int.Parse(uniqueId[2..4]) + 0x80);
        buf.WriteByte(int.Parse(uniqueId[4..6]) + 0x80);
        buf.WriteByte(int.Parse(uniqueId[6..8]));

        if (content != null)
        {
            buf.WriteBytes(content);
        }

        var bodyBytes = new byte[buf.ReadableBytes];
        buf.GetBytes(buf.ReaderIndex, bodyBytes);
        buf.WriteByte(Checksum.Xor(bodyBytes));
        buf.WriteByte(0x0D); // ending

        return buf;
    }

    protected override object? EncodeCommand(Command command)
    {
        var uniqueId = GetUniqueId(command.DeviceId);

        return command.Type switch
        {
            Command.TypeEngineStop => EncodeCommand(MsgCutOil, uniqueId, null),
            Command.TypeEngineResume => EncodeCommand(MsgResumeOil, uniqueId, null),
            Command.TypeGetVersion => EncodeCommand(MsgCheckVersion, uniqueId, null),
            Command.TypeFactoryReset => EncodeCommand(MsgFactoryReset, uniqueId, null),
            Command.TypeSetSpeedLimit => EncodeCommand(MsgResumeOil, uniqueId, EncodeSpeedLimit(command)),
            Command.TypeSetOdometer => EncodeCommand(MsgDeleteMileage, uniqueId, null),
            Command.TypePositionSingle => EncodeCommand(MsgOnDemandTrack, uniqueId, null),
            _ => null,
        };
    }

    private static IByteBuffer EncodeSpeedLimit(Command command)
    {
        var content = Unpooled.Buffer();
        content.WriteByte(command.GetInteger(Command.KeyData));
        return content;
    }
}
