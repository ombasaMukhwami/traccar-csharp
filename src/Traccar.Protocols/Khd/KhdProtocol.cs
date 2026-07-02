using DotNetty.Codecs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Khd;

public sealed class KhdProtocol : BaseProtocol
{
    public KhdProtocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory,
        IPositionForwarder? positionForwarder = null)
        : base(options, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypeGetVersion,
            Command.TypeFactoryReset,
            Command.TypeSetSpeedLimit,
            Command.TypeSetOdometer,
            Command.TypePositionSingle);

        AddServer(pipeline =>
        {
            // KHD has no protocol-specific framing; it uses a standard length-field-based frame
            // (length at offset 3, 2 bytes), matching Java's LengthFieldBasedFrameDecoder(MAX, 3, 2).
            pipeline.AddLast(new LengthFieldBasedFrameDecoder(BaseFrameDecoder.LargeMaxFrameLength, 3, 2));
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new KhdProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<KhdProtocolEncoder>()));
            pipeline.AddLast(new KhdProtocolDecoder(connectionManager, loggerFactory.CreateLogger<KhdProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
