using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.GoSafe;

public sealed class GoSafeProtocol : BaseProtocol
{
    public GoSafeProtocol(
        IConfiguration configuration, ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory)
        : base(configuration, loggerFactory)
    {
        // GoSafe has no protocol encoder in Java either - no data commands are supported.
        AddServer(pipeline =>
        {
            pipeline.AddLast(new GoSafeFrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new GoSafeProtocolDecoder(connectionManager, loggerFactory.CreateLogger<GoSafeProtocolDecoder>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });

        // UDP datagrams arrive already framed (one packet = one message), so there's no frame
        // decoder, and connections aren't tracked since UDP has no per-device socket to close.
        AddServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new GoSafeProtocolDecoder(connectionManager, loggerFactory.CreateLogger<GoSafeProtocolDecoder>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
