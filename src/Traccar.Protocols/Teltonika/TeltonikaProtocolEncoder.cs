using System.Text;
using System.Text.RegularExpressions;
using DotNetty.Buffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Helpers;
using Traccar.Storage;

namespace Traccar.Protocols.Teltonika;

public sealed class TeltonikaProtocolEncoder(IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<TeltonikaProtocolEncoder> logger)
    : BaseProtocolEncoder("teltonika", dbContextFactory, logger)
{
    private static IByteBuffer EncodeContent(byte[] content)
    {
        var buf = Unpooled.Buffer();

        buf.WriteInt(0);
        buf.WriteInt(content.Length + 8);
        buf.WriteByte(TeltonikaProtocolDecoder.Codec12);
        buf.WriteByte(1); // quantity
        buf.WriteByte(5); // type
        buf.WriteInt(content.Length);
        buf.WriteBytes(content);
        buf.WriteByte(1); // quantity

        var crcBytes = new byte[buf.ReadableBytes - 8];
        buf.GetBytes(8, crcBytes);
        buf.WriteInt(Checksum.Crc16(Checksum.Crc16Ibm, crcBytes));

        return buf;
    }

    protected override object? EncodeCommand(Command command)
    {
        switch (command.Type)
        {
            case Command.TypeEngineStop:
                return EncodeContent(Encoding.ASCII.GetBytes("setdigout 1"));
            case Command.TypeEngineResume:
                return EncodeContent(Encoding.ASCII.GetBytes("setdigout 0"));
            case Command.TypeCustom:
                var data = command.GetString(Command.KeyData) ?? string.Empty;
                return Regex.IsMatch(data, "^([0-9A-Fa-f]{2})+$")
                    ? EncodeContent(Convert.FromHexString(data))
                    : EncodeContent(Encoding.ASCII.GetBytes(data));
            default:
                return null;
        }
    }
}
