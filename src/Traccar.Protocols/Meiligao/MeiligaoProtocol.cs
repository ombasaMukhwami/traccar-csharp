using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Meiligao;

public sealed class MeiligaoProtocol : BaseProtocol
{
    public MeiligaoProtocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache, ILoggerFactory loggerFactory,
        IGeocoderService? geocoderService = null,
        IPositionForwarder? positionForwarder = null)
        : base(options, configuration, dbContextFactory, positionCache, geocoderService, positionForwarder, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypePositionSingle,
            Command.TypePositionPeriodic,
            Command.TypeOutputControl,
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypeAlarmGeofence,
            Command.TypeSetTimezone,
            Command.TypeRequestPhoto,
            Command.TypeRebootDevice);

        AddPositionServer(pipeline =>
        {
            pipeline.AddLast(new MeiligaoFrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new MeiligaoProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeiligaoProtocolEncoder>()));
            pipeline.AddLast(new MeiligaoProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeiligaoProtocolDecoder>()));
        });

        AddPositionServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new MeiligaoProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeiligaoProtocolEncoder>()));
            pipeline.AddLast(new MeiligaoProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeiligaoProtocolDecoder>()));
        });
    }
}
