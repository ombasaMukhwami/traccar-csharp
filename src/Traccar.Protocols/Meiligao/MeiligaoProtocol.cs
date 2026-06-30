using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Meiligao;

public sealed class MeiligaoProtocol : BaseProtocol
{
    public MeiligaoProtocol(
        IConfiguration configuration, ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory,
        IPositionForwarder? positionForwarder = null)
        : base(configuration, loggerFactory)
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

        AddServer(pipeline =>
        {
            pipeline.AddLast(new MeiligaoFrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new MeiligaoProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeiligaoProtocolEncoder>()));
            pipeline.AddLast(new MeiligaoProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeiligaoProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });

        // UDP datagrams arrive already framed (one packet = one message), so there's no frame
        // decoder, and connections aren't tracked since UDP has no per-device socket to close.
        AddServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new MeiligaoProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeiligaoProtocolEncoder>()));
            pipeline.AddLast(new MeiligaoProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeiligaoProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
