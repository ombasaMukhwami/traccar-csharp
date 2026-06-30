using System.Collections.Concurrent;
using System.Text;
using DotNetty.Buffers;

namespace Traccar.Protocols.Media;

/// <summary>
/// Buffers incoming JT1078 video frames per device/channel into rolling MPEG-TS segments and serves
/// an HLS playlist, mirroring Java's VideoStreamManager.
/// </summary>
public sealed class VideoStreamManager
{
    private const int MaxSegments = 5;

    private readonly ConcurrentDictionary<string, DeviceStream> streams = new();

    public void HandleFrame(long deviceId, int channel, IByteBuffer nalData, long timestamp, bool isKeyFrame, int payloadType)
        => streams.GetOrAdd(Key(deviceId, channel), _ => new DeviceStream()).AddFrame(nalData, timestamp, isKeyFrame, payloadType);

    public string GetPlaylist(long deviceId, int channel)
        => streams.TryGetValue(Key(deviceId, channel), out var stream) ? stream.GetPlaylist() : DeviceStream.EmptyPlaylist;

    public void RemoveStream(long deviceId, int channel)
    {
        if (streams.TryRemove(Key(deviceId, channel), out var stream))
        {
            stream.Release();
        }
    }

    public IByteBuffer? GetSegment(long deviceId, int channel, int index)
        => streams.TryGetValue(Key(deviceId, channel), out var stream) ? stream.GetSegment(index) : null;

    private static string Key(long deviceId, int channel) => $"{deviceId}_{channel}";

    private sealed class DeviceStream
    {
        private readonly object sync = new();
        private readonly VideoStreamWriter writer = new();
        private readonly Dictionary<int, IByteBuffer> segments = [];
        private readonly List<int> segmentOrder = [];
        private IByteBuffer? currentSegment;
        private int segmentIndex;
        private long firstTimestamp;

        public void AddFrame(IByteBuffer nalData, long timestamp, bool isKeyFrame, int payloadType)
        {
            lock (sync)
            {
                if (isKeyFrame && currentSegment != null)
                {
                    FinalizeSegment();
                }

                if (currentSegment == null)
                {
                    currentSegment = Unpooled.Buffer();
                    if (firstTimestamp == 0)
                    {
                        firstTimestamp = timestamp;
                    }
                }

                writer.Write(currentSegment, nalData, timestamp - firstTimestamp, isKeyFrame, payloadType);
            }
        }

        private void FinalizeSegment()
        {
            segments[segmentIndex] = currentSegment!;
            segmentOrder.Add(segmentIndex);
            segmentIndex++;
            currentSegment = null;

            while (segmentOrder.Count > MaxSegments)
            {
                var oldest = segmentOrder[0];
                segmentOrder.RemoveAt(0);
                segments.Remove(oldest, out var removed);
                removed?.Release();
            }
        }

        public void Release()
        {
            lock (sync)
            {
                currentSegment?.Release();
                foreach (var segment in segments.Values)
                {
                    segment.Release();
                }
            }
        }

        public const string EmptyPlaylist =
            "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:5\n#EXT-X-MEDIA-SEQUENCE:0\n";

        public string GetPlaylist()
        {
            lock (sync)
            {
                if (currentSegment != null)
                {
                    FinalizeSegment();
                }
                if (segmentOrder.Count == 0)
                {
                    return EmptyPlaylist;
                }

                var firstIndex = segmentOrder[0];

                var sb = new StringBuilder();
                sb.Append("#EXTM3U\n");
                sb.Append("#EXT-X-VERSION:3\n");
                sb.Append("#EXT-X-TARGETDURATION:5\n");
                sb.Append("#EXT-X-MEDIA-SEQUENCE:").Append(firstIndex).Append('\n');

                foreach (var key in segmentOrder)
                {
                    sb.Append("#EXTINF:3.0,\n");
                    sb.Append(key).Append(".ts\n");
                }

                return sb.ToString();
            }
        }

        public IByteBuffer? GetSegment(int index)
        {
            lock (sync)
            {
                return segments.GetValueOrDefault(index);
            }
        }
    }
}
