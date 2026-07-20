using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.H02;

public sealed class H02Protocol : BaseProtocol
{
    public H02Protocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache, ILoggerFactory loggerFactory,
        IGeocoderService? geocoderService = null,
        IPositionForwarder? positionForwarder = null)
        : base(options, configuration, dbContextFactory, positionCache, geocoderService, positionForwarder, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypeAlarmArm,
            Command.TypeAlarmDisarm,
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypePositionPeriodic);

        AddPositionServer(pipeline =>
        {
            pipeline.AddLast(new H02FrameDecoder(0));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new H02ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<H02ProtocolEncoder>()));
            pipeline.AddLast(new H02ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<H02ProtocolDecoder>()));
        });

        AddPositionServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new H02ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<H02ProtocolEncoder>()));
            pipeline.AddLast(new H02ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<H02ProtocolDecoder>()));
        });
    }
}
