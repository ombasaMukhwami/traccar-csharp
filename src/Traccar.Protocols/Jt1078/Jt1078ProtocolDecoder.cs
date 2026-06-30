using System.Net;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Helpers;
using Traccar.Protocols.Jt808;
using Traccar.Protocols.Media;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Jt1078;

/// <summary>
/// JT/T 1078 video/audio stream relay: reassembles H.264/H.265 NAL units from the device's RTP-like
/// framing and hands them to VideoStreamManager. Unlike every other ported protocol, this one never
/// produces a Position - there's no GPS data here, only video.
/// </summary>
public sealed class Jt1078ProtocolDecoder(
    ConnectionManager connectionManager, DeviceLookupService deviceLookupService,
    VideoStreamManager streamManager, ILogger<Jt1078ProtocolDecoder> logger)
    : BaseProtocolDecoder("jt1078", connectionManager, logger)
{
    private CompositeByteBuffer? frameBuffer;
    private int frameDataType;
    private long frameTimestamp;
    private int framePayloadType;

    private long streamDeviceId;
    private int streamChannel;

    protected override object? Decode(IChannel channel, EndPoint? remoteAddress, object message)
    {
        var buf = (IByteBuffer)message;

        buf.ReadUnsignedInt(); // header
        buf.ReadByte(); // V/P/X/CC
        var payloadType = buf.ReadByte() & 0x7F; // M/PT
        buf.ReadUnsignedShort(); // index

        var idLength = buf.GetUnsignedShort(buf.ReaderIndex) == 0 ? 10 : 6;
        var uniqueId = Jt808ProtocolDecoder.DecodeId(buf.ReadSlice(idLength));
        var videoChannel = buf.ReadByte();
        var rawType = buf.ReadByte();
        var dataType = BitUtil.From(rawType, 4);
        var subpackageType = BitUtil.To(rawType, 4);
        var timestamp = buf.ReadLong();

        if (dataType <= 2)
        {
            buf.SkipBytes(4); // i-frame interval + frame interval
        }

        var bodyLength = buf.ReadUnsignedShort();

        if (bodyLength == 0 || dataType > 2)
        {
            return null;
        }

        var device = deviceLookupService.LookupAsync(uniqueId).GetAwaiter().GetResult();
        if (device == null)
        {
            return null;
        }

        streamDeviceId = device.Id;
        streamChannel = videoChannel;

        var body = buf.ReadRetainedSlice(bodyLength);

        switch (subpackageType)
        {
            case 0:
                var isKeyFrame = dataType == 0;
                streamManager.HandleFrame(streamDeviceId, videoChannel, body, timestamp, isKeyFrame, payloadType);
                body.Release();
                break;
            case 1:
                frameBuffer?.Release();
                frameBuffer = Unpooled.CompositeBuffer();
                frameBuffer.AddComponent(true, body);
                frameDataType = dataType;
                frameTimestamp = timestamp;
                framePayloadType = payloadType;
                break;
            case 3:
                if (frameBuffer != null)
                {
                    frameBuffer.AddComponent(true, body);
                }
                else
                {
                    body.Release();
                }
                break;
            case 2:
                if (frameBuffer != null)
                {
                    frameBuffer.AddComponent(true, body);
                    var isKeyFrame2 = frameDataType == 0;
                    streamManager.HandleFrame(
                        streamDeviceId, videoChannel, frameBuffer, frameTimestamp, isKeyFrame2, framePayloadType);
                    frameBuffer.Release();
                    frameBuffer = null;
                }
                else
                {
                    body.Release();
                }
                break;
            default:
                body.Release();
                break;
        }

        return null;
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        base.ChannelInactive(context);
        if (streamDeviceId > 0)
        {
            streamManager.RemoveStream(streamDeviceId, streamChannel);
        }
        if (frameBuffer != null)
        {
            frameBuffer.Release();
            frameBuffer = null;
        }
    }
}
