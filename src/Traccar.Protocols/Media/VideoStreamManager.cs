using System.Collections.Concurrent;
using System.Text;
using DotNetty.Buffers;

namespace Traccar.Protocols.Media;

/// <summary>
/// Buffers incoming JT1078 video frames per device/channel into rolling MPEG-TS _segments and serves
/// an HLS playlist, mirroring Java's VideoStreamManager.
/// </summary>
public sealed class VideoStreamManager
{
    private const int MaxSegments = 5;

    private readonly ConcurrentDictionary<string, DeviceStream> _streams = new();

    public void HandleFrame(long deviceId, int channel, IByteBuffer nalData, long timestamp, bool isKeyFrame, int payloadType)
        => _streams.GetOrAdd(Key(deviceId, channel), _ => new DeviceStream()).AddFrame(nalData, timestamp, isKeyFrame, payloadType);

    public string GetPlaylist(long deviceId, int channel)
        => _streams.TryGetValue(Key(deviceId, channel), out var stream) ? stream.GetPlaylist() : DeviceStream.EmptyPlaylist;

    public void RemoveStream(long deviceId, int channel)
    {
        if (_streams.TryRemove(Key(deviceId, channel), out var stream))
        {
            stream.Release();
        }
    }

    public IByteBuffer? GetSegment(long deviceId, int channel, int index)
        => _streams.TryGetValue(Key(deviceId, channel), out var stream) ? stream.GetSegment(index) : null;

    private static string Key(long deviceId, int channel) => $"{deviceId}_{channel}";

    private sealed class DeviceStream
    {
        private readonly object _sync = new();
        private readonly VideoStreamWriter _writer = new();
        private readonly Dictionary<int, IByteBuffer> _segments = [];
        private readonly List<int> _segmentOrder = [];
        private IByteBuffer? _currentSegment;
        private int _segmentIndex;
        private long _firstTimestamp;

        public void AddFrame(IByteBuffer nalData, long timestamp, bool isKeyFrame, int payloadType)
        {
            lock (_sync)
            {
                if (isKeyFrame && _currentSegment != null)
                {
                    FinalizeSegment();
                }

                if (_currentSegment == null)
                {
                    _currentSegment = Unpooled.Buffer();
                    if (_firstTimestamp == 0)
                    {
                        _firstTimestamp = timestamp;
                    }
                }

                _writer.Write(_currentSegment, nalData, timestamp - _firstTimestamp, isKeyFrame, payloadType);
            }
        }

        private void FinalizeSegment()
        {
            _segments[_segmentIndex] = _currentSegment!;
            _segmentOrder.Add(_segmentIndex);
            _segmentIndex++;
            _currentSegment = null;

            while (_segmentOrder.Count > MaxSegments)
            {
                var oldest = _segmentOrder[0];
                _segmentOrder.RemoveAt(0);
                _segments.Remove(oldest, out var removed);
                removed?.Release();
            }
        }

        public void Release()
        {
            lock (_sync)
            {
                _currentSegment?.Release();
                foreach (var segment in _segments.Values)
                {
                    segment.Release();
                }
            }
        }

        public const string EmptyPlaylist =
            "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:5\n#EXT-X-MEDIA-SEQUENCE:0\n";

        public string GetPlaylist()
        {
            lock (_sync)
            {
                if (_currentSegment != null)
                {
                    FinalizeSegment();
                }
                if (_segmentOrder.Count == 0)
                {
                    return EmptyPlaylist;
                }

                var firstIndex = _segmentOrder[0];

                var sb = new StringBuilder();
                sb.Append("#EXTM3U\n");
                sb.Append("#EXT-X-VERSION:3\n");
                sb.Append("#EXT-X-TARGETDURATION:5\n");
                sb.Append("#EXT-X-MEDIA-SEQUENCE:").Append(firstIndex).Append('\n');

                foreach (var key in _segmentOrder)
                {
                    sb.Append("#EXTINF:3.0,\n");
                    sb.Append(key).Append(".ts\n");
                }

                return sb.ToString();
            }
        }

        public IByteBuffer? GetSegment(int index)
        {
            lock (_sync)
            {
                return _segments.GetValueOrDefault(index);
            }
        }
    }
}
