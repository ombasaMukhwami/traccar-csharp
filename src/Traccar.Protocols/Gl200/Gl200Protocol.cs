using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Gl200;

public sealed class Gl200Protocol : BaseProtocol
{
    public Gl200Protocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory,
        IPositionForwarder? positionForwarder = null)
        : base(options, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypePositionSingle,
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypeIdentification,
            Command.TypeRebootDevice);

        AddServer(pipeline =>
        {
            pipeline.AddLast(new Gl200FrameDecoder());
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new Gl200ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<Gl200ProtocolEncoder>()));
            pipeline.AddLast(new Gl200ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<Gl200ProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });

        // UDP datagrams arrive already framed (one packet = one message), so there's no frame
        // decoder, and connections aren't tracked since UDP has no per-device socket to close.
        AddServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new Gl200ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<Gl200ProtocolEncoder>()));
            pipeline.AddLast(new Gl200ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<Gl200ProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
