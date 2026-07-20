using DotNetty.Codecs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Khd;

public sealed class KhdProtocol : BaseProtocol
{
    public KhdProtocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache, ILoggerFactory loggerFactory,
        IGeocoderService? geocoderService = null,
        IPositionForwarder? positionForwarder = null)
        : base(options, configuration, dbContextFactory, positionCache, geocoderService, positionForwarder, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypeGetVersion,
            Command.TypeFactoryReset,
            Command.TypeSetSpeedLimit,
            Command.TypeSetOdometer,
            Command.TypePositionSingle);

        AddPositionServer(pipeline =>
        {
            pipeline.AddLast(new LengthFieldBasedFrameDecoder(BaseFrameDecoder.LargeMaxFrameLength, 3, 2));
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new KhdProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<KhdProtocolEncoder>()));
            pipeline.AddLast(new KhdProtocolDecoder(connectionManager, loggerFactory.CreateLogger<KhdProtocolDecoder>()));
        });
    }
}
