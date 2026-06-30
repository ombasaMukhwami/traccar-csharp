using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Teltonika;

public sealed class TeltonikaProtocol : BaseProtocol
{
    public TeltonikaProtocol(
        IConfiguration configuration, ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory,
        IPositionForwarder? positionForwarder = null)
        : base(configuration, loggerFactory)
    {
        SetSupportedDataCommands(Command.TypeCustom, Command.TypeEngineStop, Command.TypeEngineResume);

        AddServer(pipeline =>
        {
            pipeline.AddLast(new TeltonikaFrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new TeltonikaProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<TeltonikaProtocolEncoder>()));
            pipeline.AddLast(new TeltonikaProtocolDecoder(
                connectionManager, loggerFactory.CreateLogger<TeltonikaProtocolDecoder>(), configuration, connectionless: false));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });

        // UDP datagrams arrive already framed (one packet = one message), so there's no frame
        // decoder, and connections aren't tracked since UDP has no per-device socket to close.
        AddServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new TeltonikaProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<TeltonikaProtocolEncoder>()));
            pipeline.AddLast(new TeltonikaProtocolDecoder(
                connectionManager, loggerFactory.CreateLogger<TeltonikaProtocolDecoder>(), configuration, connectionless: true));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
