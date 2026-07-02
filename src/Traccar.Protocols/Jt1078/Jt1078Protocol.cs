using Microsoft.Extensions.Logging;
using Traccar.Protocols.Media;
using Traccar.Protocols.Session;

namespace Traccar.Protocols.Jt1078;

public sealed class Jt1078Protocol : BaseProtocol
{
    public Jt1078Protocol(
        ProtocolOptions options, ConnectionManager connectionManager,
        DeviceLookupService deviceLookupService, VideoStreamManager streamManager,
        ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
        AddServer(pipeline =>
        {
            pipeline.AddLast(new Jt1078FrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new Jt1078ProtocolDecoder(
                connectionManager, deviceLookupService, streamManager, loggerFactory.CreateLogger<Jt1078ProtocolDecoder>()));
        });
    }
}
