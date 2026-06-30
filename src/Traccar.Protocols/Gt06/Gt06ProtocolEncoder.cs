using System.Text;
using DotNetty.Buffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Protocols.Gt06;

public sealed class Gt06ProtocolEncoder(IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<Gt06ProtocolEncoder> logger)
    : BaseProtocolEncoder("gt06", dbContextFactory, logger)
{
    // No config system is ported yet, so per-device language/alternative-command overrides are not
    // available; these mirror Java's AttributeUtil.lookup defaults (both off).
    private const bool Language = false;
    private const bool Alternative = false;
    private const string DefaultPassword = "123456";

    private static IByteBuffer EncodeContent(string content)
    {
        var buf = Unpooled.Buffer();

        buf.WriteByte(0x78);
        buf.WriteByte(0x78);

        buf.WriteByte(1 + 1 + 4 + content.Length + 2 + 2 + (Language ? 2 : 0)); // message length

        buf.WriteByte(Gt06ProtocolDecoder.MsgCommand0);

        buf.WriteByte(4 + content.Length); // command length
        buf.WriteInt(0);
        buf.WriteBytes(Encoding.ASCII.GetBytes(content)); // command

        if (Language)
        {
            buf.WriteShort(2); // english language
        }

        buf.WriteShort(0); // message index

        var crcBytes = new byte[buf.WriterIndex - 2];
        buf.GetBytes(2, crcBytes);
        buf.WriteShort(Checksum.Crc16(Checksum.Crc16X25, crcBytes));

        buf.WriteByte('\r');
        buf.WriteByte('\n');

        return buf;
    }

    protected override object? EncodeCommand(Command command)
    {
        var model = GetDeviceModel(command.DeviceId);

        switch (command.Type)
        {
            case Command.TypeEngineStop:
                if (model == "G109")
                {
                    return EncodeContent("DYD#");
                }
                return Alternative ? EncodeContent($"DYD,{DefaultPassword}#") : EncodeContent("Relay,1#");
            case Command.TypeEngineResume:
                if (model == "G109")
                {
                    return EncodeContent("HFYD#");
                }
                return Alternative ? EncodeContent($"HFYD,{DefaultPassword}#") : EncodeContent("Relay,0#");
            case Command.TypeCustom:
                return EncodeContent(command.GetString(Command.KeyData) ?? string.Empty);
            default:
                return null;
        }
    }
}
