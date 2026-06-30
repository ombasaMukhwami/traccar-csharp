using DotNetty.Buffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Protocols.Meiligao;

public sealed class MeiligaoProtocolEncoder(
    IConfiguration configuration, IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<MeiligaoProtocolEncoder> logger)
    : BaseProtocolEncoder("meiligao", dbContextFactory, logger)
{
    private static readonly HashSet<string> MultiOutputModels = ["TK510", "GT08", "TK208", "TK228", "MT05"];

    private IByteBuffer EncodeContent(long deviceId, int type, IByteBuffer content)
    {
        var buf = Unpooled.Buffer();

        buf.WriteByte('@');
        buf.WriteByte('@');

        buf.WriteShort(2 + 2 + 7 + 2 + content.ReadableBytes + 2 + 2); // message length

        buf.WriteBytes(Convert.FromHexString((GetUniqueId(deviceId) + "FFFFFFFFFFFFFF")[..14]));

        buf.WriteShort(type);

        buf.WriteBytes(content);

        var checksumBytes = new byte[buf.ReadableBytes];
        buf.GetBytes(buf.ReaderIndex, checksumBytes);
        buf.WriteShort(Checksum.Crc16(Checksum.Crc16CcittFalse, checksumBytes));

        buf.WriteByte('\r');
        buf.WriteByte('\n');

        return buf;
    }

    /// <summary>Parses a "GMT+8"/"GMT-5"-style offset string into minutes, matching Java's TimeZone.getTimeZone behavior.</summary>
    private static int ParseGmtOffsetMinutes(string id)
    {
        if (id.StartsWith("GMT", StringComparison.OrdinalIgnoreCase) && id.Length > 3
            && double.TryParse(id.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture, out var hours))
        {
            var sign = id[3] == '-' ? -1 : 1;
            return (int)(sign * hours * 60);
        }
        return (int)TimeZoneInfo.FindSystemTimeZoneById(id).BaseUtcOffset.TotalMinutes;
    }

    private IByteBuffer EncodeOutputCommand(long deviceId, int index, int value)
    {
        int outputCount;
        int outputType;

        var model = GetDeviceModel(deviceId);

        if (model != null && MultiOutputModels.Contains(model))
        {
            outputCount = 5;
            outputType = MeiligaoProtocolDecoder.MsgOutputControl1;
        }
        else
        {
            outputCount = 1;
            var alternative = configuration.GetValue<bool>("Protocols:meiligao:Alternative");
            outputType = alternative ? MeiligaoProtocolDecoder.MsgOutputControl1 : MeiligaoProtocolDecoder.MsgOutputControl2;
        }

        var content = Unpooled.Buffer();

        for (var i = 1; i <= outputCount; i++)
        {
            content.WriteByte(i == index ? value : 2);
        }

        return EncodeContent(deviceId, outputType, content);
    }

    protected override object? EncodeCommand(Command command)
    {
        var content = Unpooled.Buffer();

        switch (command.Type)
        {
            case Command.TypePositionSingle:
                return EncodeContent(command.DeviceId, MeiligaoProtocolDecoder.MsgTrackOnDemand, content);
            case Command.TypePositionPeriodic:
                content.WriteShort(command.GetInteger(Command.KeyFrequency) / 10);
                return EncodeContent(command.DeviceId, MeiligaoProtocolDecoder.MsgTrackByInterval, content);
            case Command.TypeOutputControl:
                var index = command.GetInteger(Command.KeyIndex) - 1;
                var value = command.GetInteger(Command.KeyData);
                return EncodeOutputCommand(command.DeviceId, index, value);
            case Command.TypeEngineStop:
                return EncodeOutputCommand(command.DeviceId, 1, 1);
            case Command.TypeEngineResume:
                return EncodeOutputCommand(command.DeviceId, 1, 0);
            case Command.TypeAlarmGeofence:
                content.WriteShort(command.GetInteger(Command.KeyRadius));
                return EncodeContent(command.DeviceId, MeiligaoProtocolDecoder.MsgMovementAlarm, content);
            case Command.TypeSetTimezone:
                var offset = ParseGmtOffsetMinutes(command.GetString(Command.KeyTimezone)!);
                content.WriteBytes(System.Text.Encoding.ASCII.GetBytes(offset.ToString()));
                return EncodeContent(command.DeviceId, MeiligaoProtocolDecoder.MsgTimeZone, content);
            case Command.TypeRequestPhoto:
                return EncodeContent(command.DeviceId, MeiligaoProtocolDecoder.MsgTakePhoto, content);
            case Command.TypeRebootDevice:
                return EncodeContent(command.DeviceId, MeiligaoProtocolDecoder.MsgRebootGps, content);
            default:
                return null;
        }
    }
}
