using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.H02;

public sealed class H02Protocol : BaseProtocol
{
    public H02Protocol(
        IConfiguration configuration, ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory,
        IPositionForwarder? positionForwarder = null)
        : base(configuration, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypeAlarmArm,
            Command.TypeAlarmDisarm,
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypePositionPeriodic);

        AddServer(pipeline =>
        {
            pipeline.AddLast(new H02FrameDecoder(0));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new H02ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<H02ProtocolEncoder>()));
            pipeline.AddLast(new H02ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<H02ProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });

        // UDP datagrams arrive already framed (one packet = one message), so there's no frame
        // decoder, and connections aren't tracked since UDP has no per-device socket to close.
        AddServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new H02ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<H02ProtocolEncoder>()));
            pipeline.AddLast(new H02ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<H02ProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
