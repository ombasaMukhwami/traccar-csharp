using DotNetty.Transport.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Handlers;
using Traccar.Protocols.Handlers.Events;
using Traccar.Protocols.Session;
using Traccar.Storage;
using Traccar.Model;

namespace Traccar.Protocols;

public abstract class BaseProtocol : IProtocol
{
    /// <summary>Derived from the class name like Java's BaseProtocol.nameFromClass (Gt06Protocol -> "gt06").</summary>
    public string Name { get; }

    private readonly ProtocolOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _supportedDataCommands = [];
    private readonly List<ITrackerConnector> _connectorList = [];

    // Optional — set by the position-protocol constructor overload.
    private IConfiguration? _configuration;
    private IDbContextFactory<TraccarDbContext>? _dbContextFactory;
    private PositionCache? _positionCache;
    private IGeocoderService? _geocoderService;
    private IPositionForwarder? _positionForwarder;
    private OutdatedHandler? _outdatedHandler;
    private ComputedAttributesHandler? _computedEarlyHandler;
    private CopyAttributesHandler? _copyAttributesHandler;
    private DistanceHandler? _distanceHandler;
    private EngineHoursHandler? _engineHoursHandler;
    private MotionHandler? _motionHandler;
    private ComputedAttributesHandler? _computedLateHandler;
    private FilterHandler? _filterHandler;
    private PositionAttributesHandler? _attributesHandler;
    private AlarmEventHandler? _alarmEventHandler;
    private ProximityEventHandler? _proximityEventHandler;

    /// <summary>
    /// Base constructor for protocols that do NOT produce Position objects (e.g. media/video).
    /// </summary>
    protected BaseProtocol(ProtocolOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        Name = NameFromType(GetType());
    }

    /// <summary>
    /// Base constructor for protocols that produce Position objects. Stores the shared
    /// post-processing dependencies so <see cref="AddPositionServer"/> can append the standard
    /// PositionProcessingHandler → GeocoderHandler → PositionForwardingHandler → PositionPersistHandler tail.
    /// </summary>
    protected BaseProtocol(
        ProtocolOptions options,
        IConfiguration configuration,
        IDbContextFactory<TraccarDbContext> dbContextFactory,
        PositionCache positionCache,
        IGeocoderService? geocoderService,
        IPositionForwarder? positionForwarder,
        ILoggerFactory loggerFactory)
        : this(options, loggerFactory)
    {
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _positionCache = positionCache;
        _geocoderService = geocoderService;
        _positionForwarder = positionForwarder;

        var attrCache = new DeviceAttributeCache(dbContextFactory);
        var computedProvider = new ComputedAttributesProvider(configuration);
        var computedLog = loggerFactory.CreateLogger<ComputedAttributesHandler>();

        _outdatedHandler = new OutdatedHandler();
        _computedEarlyHandler = new ComputedAttributesHandler(attrCache, computedProvider, early: true, computedLog);
        _copyAttributesHandler = new CopyAttributesHandler(configuration);
        _distanceHandler = new DistanceHandler(configuration);
        _engineHoursHandler = new EngineHoursHandler();
        _motionHandler = new MotionHandler(configuration);
        _computedLateHandler = new ComputedAttributesHandler(attrCache, computedProvider, early: false, computedLog);
        _filterHandler = new FilterHandler(configuration, loggerFactory.CreateLogger<FilterHandler>());
        _attributesHandler = new PositionAttributesHandler();
        _alarmEventHandler = new AlarmEventHandler(configuration, loggerFactory.CreateLogger<AlarmEventHandler>());
        _proximityEventHandler = new ProximityEventHandler(dbContextFactory, positionCache, loggerFactory.CreateLogger<ProximityEventHandler>());
    }

    public static string NameFromType(Type type)
    {
        var className = type.Name;
        return (className.EndsWith("Protocol", StringComparison.Ordinal)
            ? className[..^"Protocol".Length]
            : className).ToLowerInvariant();
    }

    public IReadOnlyCollection<string> SupportedDataCommands => _supportedDataCommands;

    protected void SetSupportedDataCommands(params string[] commands) => _supportedDataCommands.AddRange(commands);

    public IReadOnlyCollection<ITrackerConnector> ConnectorList => _connectorList;

    /// <summary>Registers a TCP listener using the raw pipeline action (no position tail appended).</summary>
    protected void AddServer(Action<IChannelPipeline> configurePipeline)
        => AddServer(datagram: false, configurePipeline);

    protected void AddServer(bool datagram, Action<IChannelPipeline> configurePipeline)
    {
        _connectorList.Add(new ProtocolServer(
            _options.Name, _options.Port, datagram, _options.Timeout, configurePipeline,
            _loggerFactory.CreateLogger<ProtocolServer>()));
    }

    /// <summary>
    /// Registers a TCP listener and automatically appends the standard position-processing tail:
    /// PositionProcessingHandler (enrich + filter) → [optional GeocoderHandler] → PositionForwardingHandler → PositionPersistHandler.
    /// </summary>
    protected void AddPositionServer(Action<IChannelPipeline> configurePipeline)
        => AddPositionServer(datagram: false, configurePipeline);

    protected void AddPositionServer(bool datagram, Action<IChannelPipeline> configurePipeline)
    {
        // Capture locals so the lambda doesn't close over 'this' in an unexpected way.
        var positionCache = _positionCache!;
        var outdated = _outdatedHandler!;
        var computedEarly = _computedEarlyHandler!;
        var copyAttrs = _copyAttributesHandler!;
        var distance = _distanceHandler!;
        var engineHours = _engineHoursHandler!;
        var motion = _motionHandler!;
        var computedLate = _computedLateHandler!;
        var filter = _filterHandler!;
        var attributes = _attributesHandler!;
        var geocoder = _geocoderService;
        var forwarder = _positionForwarder;
        var dbFactory = _dbContextFactory!;
        var config = _configuration!;
        var logFactory = _loggerFactory;
        IReadOnlyList<BaseEventHandler> eventHandlers = [_alarmEventHandler!, _proximityEventHandler!];

        _connectorList.Add(new ProtocolServer(
            _options.Name, _options.Port, datagram, _options.Timeout,
            pipeline =>
            {
                configurePipeline(pipeline);
                pipeline.AddLast(new PositionProcessingHandler(
                    positionCache, outdated, computedEarly, copyAttrs, distance, engineHours, motion, computedLate, filter, attributes));

                var forwardingHandler = new PositionForwardingHandler(forwarder, dbFactory, config,
                    logFactory.CreateLogger<PositionForwardingHandler>());
                var persistHandler = new PositionPersistHandler(dbFactory, positionCache,
                    logFactory.CreateLogger<PositionPersistHandler>());
                var eventProcessor = new EventProcessingHandler(eventHandlers, positionCache, dbFactory,
                    logFactory.CreateLogger<EventProcessingHandler>());

                // forward -> persist -> analyze-events, invoked as plain delegate calls rather
                // than further pipeline stages, since GeocoderHandler needs to be able to resume
                // this segment from a background thread pool thread after its async lookup — see
                // GeocoderHandler's own doc comment for why that rules out further
                // context.FireChannelRead-based stages.
                void ContinuePosition(Position position)
                {
                    forwardingHandler.Process(position);
                    persistHandler.Process(position, eventProcessor.Process);
                }

                pipeline.AddLast(new GeocoderHandler(geocoder, positionCache, config,
                    logFactory.CreateLogger<GeocoderHandler>(), ContinuePosition));
            },
            _loggerFactory.CreateLogger<ProtocolServer>()));
    }
}
