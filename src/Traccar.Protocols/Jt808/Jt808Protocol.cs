using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Jt808;

public sealed class Jt808Protocol : BaseProtocol
{
    public Jt808Protocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory, ILoggerFactory loggerFactory,
        IPositionForwarder? positionForwarder = null)
        : base(options, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypeCustom,
            Command.TypeRebootDevice,
            Command.TypePositionPeriodic,
            Command.TypeAlarmArm,
            Command.TypeAlarmDisarm,
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypeVideoStart,
            Command.TypeVideoStop);

        AddServer(pipeline =>
        {
            pipeline.AddLast(new Jt808FrameEncoder());
            pipeline.AddLast(new Jt808FrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new Jt808ProtocolEncoder(configuration, dbContextFactory, loggerFactory.CreateLogger<Jt808ProtocolEncoder>()));
            pipeline.AddLast(new Jt808ProtocolDecoder(connectionManager, configuration, loggerFactory.CreateLogger<Jt808ProtocolDecoder>()));
            pipeline.AddLast(new PositionForwardingHandler(positionForwarder, dbContextFactory, configuration, loggerFactory.CreateLogger<PositionForwardingHandler>()));
            pipeline.AddLast(new PositionPersistHandler(dbContextFactory, loggerFactory.CreateLogger<PositionPersistHandler>()));
        });
    }
}
