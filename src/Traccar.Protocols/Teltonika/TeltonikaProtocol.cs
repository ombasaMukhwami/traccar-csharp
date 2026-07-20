using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Model;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Session;
using Traccar.Storage;

namespace Traccar.Protocols.Teltonika;

public sealed class TeltonikaProtocol : BaseProtocol
{
    public TeltonikaProtocol(
        ProtocolOptions options, IConfiguration configuration,
        ConnectionManager connectionManager,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache, ILoggerFactory loggerFactory,
        IGeocoderService? geocoderService = null,
        IPositionForwarder? positionForwarder = null)
        : base(options, configuration, dbContextFactory, positionCache, geocoderService, positionForwarder, loggerFactory)
    {
        SetSupportedDataCommands(Command.TypeCustom, Command.TypeEngineStop, Command.TypeEngineResume);

        AddPositionServer(pipeline =>
        {
            pipeline.AddLast(new TeltonikaFrameDecoder());
            pipeline.AddLast(new ConnectionTrackingHandler(connectionManager, loggerFactory.CreateLogger<ConnectionTrackingHandler>()));
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new TeltonikaProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<TeltonikaProtocolEncoder>()));
            pipeline.AddLast(new TeltonikaProtocolDecoder(
                connectionManager, loggerFactory.CreateLogger<TeltonikaProtocolDecoder>(), configuration, connectionless: false));
        });

        AddPositionServer(datagram: true, pipeline =>
        {
            pipeline.AddLast(new RawDataLoggingHandler(Name, loggerFactory.CreateLogger<RawDataLoggingHandler>()));
            pipeline.AddLast(new TeltonikaProtocolEncoder(dbContextFactory, loggerFactory.CreateLogger<TeltonikaProtocolEncoder>()));
            pipeline.AddLast(new TeltonikaProtocolDecoder(
                connectionManager, loggerFactory.CreateLogger<TeltonikaProtocolDecoder>(), configuration, connectionless: true));
        });
    }
}
