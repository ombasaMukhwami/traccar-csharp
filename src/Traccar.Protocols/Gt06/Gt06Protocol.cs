using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Gt06;

public sealed class Gt06Protocol : BaseProtocol
{
    public Gt06Protocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache, ILoggerFactory loggerFactory,
        IGeocoderService? geocoderService = null,
        IPositionForwarder? positionForwarder = null)
        : base(options, configuration, dbContextFactory, positionCache, geocoderService, positionForwarder, loggerFactory)
    {
        SetSupportedDataCommands(Command.TypeEngineStop, Command.TypeEngineResume, Command.TypeCustom);

        AddPositionServer(pipeline =>
        {
            pipeline.AddLast(new Gt06FrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new Gt06ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<Gt06ProtocolEncoder>()));
            pipeline.AddLast(new Gt06ProtocolDecoder(connectionManager, loggerFactory.CreateLogger<Gt06ProtocolDecoder>()));
        });
    }
}
