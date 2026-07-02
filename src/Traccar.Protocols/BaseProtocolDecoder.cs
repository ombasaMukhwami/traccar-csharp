using System.Net;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Session;

namespace Traccar.Protocols;

public abstract class BaseProtocolDecoder(
    string protocolName, ConnectionManager connectionManager, ILogger logger) : ChannelHandlerAdapter
{
    public string ProtocolName { get; } = protocolName;

    protected ConnectionManager ConnectionManager { get; } = connectionManager;

    protected ILogger Logger { get; } = logger;

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        try
        {
            // UDP channels are unconnected (shared by every sender), so DotNetty delivers a
            // DatagramPacket carrying the per-packet sender address instead of raw content; TCP
            // channels deliver content directly and have a single connected peer.
            object actualMessage = message;
            EndPoint? remoteAddress;
            if (message is DatagramPacket packet)
            {
                actualMessage = packet.Content;
                remoteAddress = packet.Sender;
            }
            else
            {
                remoteAddress = context.Channel.RemoteAddress;
            }

            object? decoded;
            try
            {
                decoded = Decode(context.Channel, remoteAddress, actualMessage);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "{Protocol} decoding error", ProtocolName);
                decoded = null;
            }

            OnMessageEvent(context.Channel, remoteAddress, decoded);

            switch (decoded)
            {
                case null:
                    break;
                case IEnumerable<Position> positions:
                    foreach (var position in positions)
                    {
                        context.FireChannelRead(position);
                    }
                    break;
                default:
                    context.FireChannelRead(decoded);
                    break;
            }
        }
        finally
        {
            ReferenceCountUtil.Release(message);
        }
    }

    protected abstract object? Decode(IChannel channel, EndPoint? remoteAddress, object message);

    protected DeviceSession? GetDeviceSession(IChannel channel, EndPoint? remoteAddress, params string?[] uniqueIds)
        => ConnectionManager.GetDeviceSessionAsync(channel, remoteAddress, uniqueIds).GetAwaiter().GetResult();

    /// <summary>
    /// Reports this server's own address (as seen by the device's connection), formatted with a
    /// custom delimiter. Simplified from Java's version, which also accepts a Server:Protocol-prefixed
    /// config override - dropped here since no such config exists in this port.
    /// </summary>
    protected static string? GetServer(IChannel? channel, char delimiter)
    {
        if (channel?.LocalAddress is IPEndPoint address)
        {
            return $"{address.Address}:{address.Port}".Replace(':', delimiter);
        }
        return null;
    }

    /// <summary>
    /// Writes a response back to the device. UDP sockets are unconnected (shared by every sender),
    /// so a reply must be addressed via a DatagramPacket to the sender of the packet it answers; TCP
    /// channels have a single implicit peer and can be written to directly. Mirrors Java's
    /// NetworkMessageHandler.write, which wraps NetworkMessage in a DatagramPacket only for
    /// DatagramChannel instances.
    /// </summary>
    protected static void WriteResponse(IChannel channel, EndPoint? remoteAddress, IByteBuffer response)
    {
        if (channel is IDatagramChannel && remoteAddress != null)
        {
            channel.WriteAndFlushAsync(new DatagramPacket(response, remoteAddress));
        }
        else
        {
            channel.WriteAndFlushAsync(response);
        }
    }

    private string? _modelOverride;

    /// <summary>Forces GetDeviceModel to report this model regardless of the device's actual one.</summary>
    public void SetModelOverride(string? modelOverride) => _modelOverride = modelOverride;

    protected string? GetDeviceModel(DeviceSession deviceSession) => _modelOverride ?? deviceSession.Model;

    /// <summary>
    /// Resolves a fallback timezone for decoding local-time-encoded fields (e.g. JT808's BCD
    /// timestamps). Simplified from Java's per-device decoder.timeZone attribute override - which
    /// relies on the CacheManager/AttributeUtil subsystem this port doesn't have - down to just the
    /// protocol-supplied default, parsed as a "GMT+8"/"GMT-5"-style offset.
    /// </summary>
    protected static TimeZoneInfo GetTimeZone(string defaultTimeZone)
    {
        if (defaultTimeZone.StartsWith("GMT", StringComparison.OrdinalIgnoreCase) && defaultTimeZone.Length > 3
            && double.TryParse(defaultTimeZone.AsSpan(4), out var hours))
        {
            var sign = defaultTimeZone[3] == '-' ? -1 : 1;
            return TimeZoneInfo.CreateCustomTimeZone(defaultTimeZone, TimeSpan.FromHours(sign * hours), defaultTimeZone, defaultTimeZone);
        }
        return TimeZoneInfo.Utc;
    }

    private IByteBuffer? _mediaBuffer;

    protected IByteBuffer? GetMediaBuffer() => _mediaBuffer;

    protected IByteBuffer NewMediaBuffer(int size = 0)
    {
        ReleaseMediaBuffer();
        _mediaBuffer = Unpooled.Buffer(size);
        return _mediaBuffer;
    }

    private void ReleaseMediaBuffer()
    {
        _mediaBuffer?.Release();
        _mediaBuffer = null;
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        ReleaseMediaBuffer();
        context.FireChannelInactive();
    }

    /// <summary>
    /// Persists the accumulated media buffer to disk and returns the stored file name. Media storage
    /// isn't wired up in this port (no Server:MediaPath setting) - matches Java's MediaManager.writeFile
    /// behavior when Keys.MEDIA_PATH is unset, which no-ops and returns null.
    /// </summary>
    protected string? WriteMediaFile(string uniqueId, string extension)
    {
        ReleaseMediaBuffer();
        return null;
    }

    protected void GetLastLocation(Position position, DateTime? deviceTime)
    {
        if (position.DeviceId != 0)
        {
            position.Outdated = true;
            if (deviceTime.HasValue)
            {
                position.DeviceTime = deviceTime;
            }
        }
    }

    private void OnMessageEvent(IChannel channel, EndPoint? remoteAddress, object? decoded)
    {
        long? deviceId = decoded switch
        {
            Position position => position.DeviceId,
            IEnumerable<Position> positions => positions.Select(p => p.DeviceId).FirstOrDefault(),
            _ => null,
        };
        deviceId ??= GetDeviceSession(channel, remoteAddress)?.DeviceId;
        if (deviceId is > 0)
        {
            _ = ConnectionManager.UpdateDeviceStatusAsync(deviceId.Value, Device.StatusOnline, DateTime.UtcNow);
        }
    }
}
