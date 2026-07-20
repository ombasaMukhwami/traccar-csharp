using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Meitrack;

public sealed class MeitrackProtocol : BaseProtocol
{
    public MeitrackProtocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache, ILoggerFactory loggerFactory,
        IGeocoderService? geocoderService = null,
        IPositionForwarder? positionForwarder = null)
        : base(options, configuration, dbContextFactory, positionCache, geocoderService, positionForwarder, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypeCustom,
            Command.TypePositionSingle,
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypeAlarmArm,
            Command.TypeAlarmDisarm,
            Command.TypeRequestPhoto,
            Command.TypeSendSms);

        AddPositionServer(pipeline =>
        {
            pipeline.AddLast(new MeitrackFrameDecoder());
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new MeitrackProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeitrackProtocolEncoder>()));
            pipeline.AddLast(new MeitrackProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeitrackProtocolDecoder>()));
        });

        AddPositionServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new MeitrackProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeitrackProtocolEncoder>()));
            pipeline.AddLast(new MeitrackProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeitrackProtocolDecoder>()));
        });
    }
}
