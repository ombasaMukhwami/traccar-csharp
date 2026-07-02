using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Storage;

namespace Traccar.Protocols.Jt808;

public sealed class Jt808ProtocolEncoder(
    IConfiguration configuration, IDbContextFactory<TraccarDbContext> dbContextFactory, ILogger<Jt808ProtocolEncoder> logger)
    : BaseProtocolEncoder("jt808", dbContextFactory, logger)
{
    private static readonly HashSet<string> AtCommandModels = ["AL300", "GL100", "VL300"];
    private static readonly HashSet<string> TextMessageModels = ["BSJ", "C5", "C5L"];

    protected override object? EncodeCommand(IChannel channel, Command command)
    {
        var decoder = channel.Pipeline.Get<Jt808ProtocolDecoder>()
            ?? throw new InvalidOperationException("Jt808ProtocolDecoder not found in pipeline");

        var alternative = configuration.GetValue<bool>($"{ConfigKeys.Protocols.SectionPrefix}:jt808:{ConfigKeys.Protocols.Alternative}");

        var protocolVersion = decoder.ProtocolVersion;
        var id = Jt808ProtocolDecoder.EncodeId(GetUniqueId(command.DeviceId), protocolVersion != null ? 10 : 6);
        var model = GetDeviceModel(command.DeviceId);
        try
        {
            var data = Unpooled.Buffer();

            switch (command.Type)
            {
                case Command.TypeCustom:
                    if (model != null && AtCommandModels.Contains(model))
                    {
                        data.WriteByte(1); // number of parameters
                        data.WriteInt(0xF030); // AT command transparent transmission
                        var text = command.GetString(Command.KeyData) ?? string.Empty;
                        data.WriteByte(text.Length);
                        data.WriteBytes(Encoding.ASCII.GetBytes(text));
                        return decoder.FormatMessage(Jt808ProtocolDecoder.MsgConfigurationParameters, id, false, data);
                    }
                    else if (model != null && TextMessageModels.Contains(model))
                    {
                        data.WriteByte(1); // flag
                        var charset = GetGbkOrAscii();
                        data.WriteBytes(charset.GetBytes(command.GetString(Command.KeyData) ?? string.Empty));
                        return decoder.FormatMessage(Jt808ProtocolDecoder.MsgSendTextMessage, id, false, data);
                    }
                    else if (model != null && model.StartsWith("JC"))
                    {
                        data.WriteByte(0xF0); // online command
                        data.WriteBytes(Encoding.ASCII.GetBytes(command.GetString(Command.KeyData) ?? string.Empty));
                        return decoder.FormatMessage(Jt808ProtocolDecoder.MsgTransparentDownlink, id, false, data);
                    }
                    else
                    {
                        return Unpooled.WrappedBuffer(Convert.FromHexString(command.GetString(Command.KeyData) ?? string.Empty));
                    }
                case Command.TypeRebootDevice:
                    data.WriteByte(1); // number of parameters
                    data.WriteByte(0x23); // parameter id
                    data.WriteByte(1); // parameter value length
                    data.WriteByte(0x03); // restart
                    return decoder.FormatMessage(Jt808ProtocolDecoder.MsgParameterSetting, id, false, data);
                case Command.TypePositionPeriodic:
                    data.WriteByte(1); // number of parameters
                    data.WriteByte(0x06); // parameter id
                    data.WriteByte(4); // parameter value length
                    data.WriteInt(command.GetInteger(Command.KeyFrequency));
                    return decoder.FormatMessage(Jt808ProtocolDecoder.MsgParameterSetting, id, false, data);
                case Command.TypeAlarmArm:
                case Command.TypeAlarmDisarm:
                    data.WriteByte(1); // number of parameters
                    data.WriteByte(0x24); // parameter id
                    const string username = "user";
                    data.WriteByte(1 + username.Length); // parameter value length
                    data.WriteByte(command.Type == Command.TypeAlarmArm ? 0x01 : 0x00);
                    data.WriteBytes(Encoding.ASCII.GetBytes(username));
                    return decoder.FormatMessage(Jt808ProtocolDecoder.MsgParameterSetting, id, false, data);
                case Command.TypeEngineStop:
                case Command.TypeEngineResume:
                    if (alternative)
                    {
                        data.WriteByte(command.Type == Command.TypeEngineStop ? 0x01 : 0x00);
                        data.WriteBytes(Convert.FromHexString(DateTime.UtcNow.ToString("yyMMddHHmmss")));
                        return decoder.FormatMessage(Jt808ProtocolDecoder.MsgOilControl, id, false, data);
                    }
                    else
                    {
                        if (model == "VL300")
                        {
                            data.WriteBytes(Encoding.ASCII.GetBytes(command.Type == Command.TypeEngineStop ? "#0;1" : "#0;0"));
                        }
                        else if (model == "W15L")
                        {
                            data.WriteByte(command.Type == Command.TypeEngineStop ? 0x64 : 0x65);
                        }
                        else
                        {
                            data.WriteByte(command.Type == Command.TypeEngineStop ? 0xf0 : 0xf1);
                        }
                        return decoder.FormatMessage(Jt808ProtocolDecoder.MsgTerminalControl, id, false, data);
                    }
                case Command.TypeVideoStart:
                    {
                        var webUrl = configuration.GetValue<string>(ConfigKeys.Server.WebUrl);
                        var host = webUrl != null ? new Uri(webUrl).Host : null;
                        var port = configuration.GetValue<int?>($"{ConfigKeys.Protocols.SectionPrefix}:jt1078:{ConfigKeys.Protocols.Port}") ?? 0;
                        var videoChannel = command.GetInteger(Command.KeyIndex, 1);
                        var hostBytes = Encoding.ASCII.GetBytes(host ?? string.Empty);
                        data.WriteByte(hostBytes.Length);
                        data.WriteBytes(hostBytes);
                        data.WriteShort(port); // tcp port
                        data.WriteShort(0); // udp port
                        data.WriteByte(videoChannel);
                        data.WriteByte(1); // video only
                        data.WriteByte(0); // main stream
                        return decoder.FormatMessage(Jt808ProtocolDecoder.MsgVideoRequest, id, false, data);
                    }
                case Command.TypeVideoStop:
                    data.WriteByte(command.GetInteger(Command.KeyIndex, 1));
                    data.WriteByte(0); // close audio/video transmission
                    data.WriteByte(0); // close both audio and video
                    data.WriteByte(0); // main stream
                    return decoder.FormatMessage(Jt808ProtocolDecoder.MsgVideoControl, id, false, data);
                default:
                    return null;
            }
        }
        finally
        {
            id.Release();
        }
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
}
