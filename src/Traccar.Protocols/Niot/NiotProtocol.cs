using DotNetty.Codecs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Niot;

public sealed class NiotProtocol : BaseProtocol
{
    public NiotProtocol(
        IConfiguration configuration, ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory)
        : base(configuration, loggerFactory)
    {
        AddServer(pipeline =>
        {
            pipeline.AddLast(new LengthFieldBasedFrameDecoder(BaseFrameDecoder.DefaultMaxFrameLength, 3, 2));
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new NiotProtocolDecoder(connectionManager, loggerFactory.CreateLogger<NiotProtocolDecoder>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
