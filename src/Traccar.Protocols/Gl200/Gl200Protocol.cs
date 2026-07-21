using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Gl200;

public sealed class Gl200Protocol : BaseProtocol
{
    public Gl200Protocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache, ILoggerFactory loggerFactory,
        IGeocoderService? geocoderService = null,
        IPositionForwarder? positionForwarder = null)
        : base(options, configuration, dbContextFactory, positionCache, geocoderService, positionForwarder, loggerFactory)
    {
        SetSupportedDataCommands(
            Command.TypePositionSingle,
            Command.TypeEngineStop,
            Command.TypeEngineResume,
            Command.TypeIdentification,
            Command.TypeRebootDevice);

        AddPositionServer(pipeline =>
        {
            pipeline.AddLast(new Gl200FrameDecoder());
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new Gl200ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<Gl200ProtocolEncoder>()));
            pipeline.AddLast(new Gl200ProtocolDecoder(connectionManager, configuration, loggerFactory));
        });

        AddPositionServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new StringEncoderHandler());
            pipeline.AddLast(new Gl200ProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<Gl200ProtocolEncoder>()));
            pipeline.AddLast(new Gl200ProtocolDecoder(connectionManager, configuration, loggerFactory));
        });
    }
}
