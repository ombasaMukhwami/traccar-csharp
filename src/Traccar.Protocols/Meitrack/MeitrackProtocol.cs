using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Meitrack;

public sealed class MeitrackProtocol : BaseProtocol
{
    public MeitrackProtocol(
        IConfiguration configuration, ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory,
        IPositionForwarder? positionForwarder = null)
        : base(configuration, loggerFactory)
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

        AddServer(pipeline =>
        {
            pipeline.AddLast(new MeitrackFrameDecoder());
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new MeitrackProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeitrackProtocolEncoder>()));
            pipeline.AddLast(new MeitrackProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeitrackProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });

        // UDP datagrams arrive already framed (one packet = one message), so there's no frame
        // decoder, and connections aren't tracked since UDP has no per-device socket to close.
        AddServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new MeitrackProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<MeitrackProtocolEncoder>()));
            pipeline.AddLast(new MeitrackProtocolDecoder(connectionManager, loggerFactory.CreateLogger<MeitrackProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
