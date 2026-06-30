using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Gt06;

public sealed class Gt06Protocol : BaseProtocol
{
    public Gt06Protocol(
        IConfiguration configuration, ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory)
        : base(configuration, loggerFactory)
    {
        SetSupportedDataCommands(Command.TypeEngineStop, Command.TypeEngineResume, Command.TypeCustom);

        AddServer(pipeline =>
        {
            pipeline.AddLast(new Gt06FrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new Gt06ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<Gt06ProtocolEncoder>()));
            pipeline.AddLast(new Gt06ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<Gt06ProtocolDecoder>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
