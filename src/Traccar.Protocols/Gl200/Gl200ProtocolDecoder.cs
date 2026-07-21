using System.Net;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Gl200;

public sealed class Gl200ProtocolDecoder : BaseProtocolDecoder
{
    private readonly Gl200TextProtocolDecoder _textProtocolDecoder;
    private readonly Gl200BinaryProtocolDecoder _binaryProtocolDecoder;

    public Gl200ProtocolDecoder(
        ConnectionManager connectionManager, IConfiguration configuration, ILoggerFactory loggerFactory)
        : base("gl200", connectionManager, loggerFactory.CreateLogger<Gl200ProtocolDecoder>())
    {
        _textProtocolDecoder = new Gl200TextProtocolDecoder(
            connectionManager, configuration, loggerFactory.CreateLogger<Gl200TextProtocolDecoder>());
        _binaryProtocolDecoder = new Gl200BinaryProtocolDecoder(
            connectionManager, loggerFactory.CreateLogger<Gl200BinaryProtocolDecoder>());
    }

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = new ByteBuf((IByteBuffer)message);

        return Gl200FrameDecoder.IsBinary(buf)
            ? _binaryProtocolDecoder.DecodeMessage(channel, remoteAddress, message)
            : _textProtocolDecoder.DecodeMessage(channel, remoteAddress, message);
    }
}
