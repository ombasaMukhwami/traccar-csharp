using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Geocoder;
using Traccar.Protocols.Media;
using Traccar.Protocols.Session;

namespace Traccar.Protocols;

public static class DependencyInjection
{
    /// <summary>
    /// Builds the external position forwarder selected by Forward:Type ("kafka", "rabbitmq", or
    /// "signalr" for pushing to a remote hub) — null if Forward:Type is unset. Exposed separately
    /// from <see cref="AddTraccarPositionForwarding"/> so a host (e.g. Traccar.Server) can compose
    /// this optional external forwarder together with its own local notifiers via
    /// <see cref="CompositePositionForwarder"/>.
    /// </summary>
    public static IPositionForwarder? CreateConfiguredForwarder(IConfiguration configuration) =>
        configuration[ConfigKeys.Forward.Type] switch
        {
            ConfigKeys.Forward.TypeKafka => new PositionForwarderKafka(configuration),
            ConfigKeys.Forward.TypeRabbitMq => new PositionForwarderRabbitMq(configuration),
            ConfigKeys.Forward.TypeSignalR => new PositionForwarderSignalR(configuration),
            _ => null,
        };

    /// <summary>
    /// Registers the position forwarder selected by Forward:Type, mirroring Java's
    /// MainModule.providePositionForwarder - only one forwarder is active at a time, and none is
    /// registered (PositionForwardingHandler becomes a no-op) if Forward:Type is unset.
    /// </summary>
    public static IServiceCollection AddTraccarPositionForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        var forwarder = CreateConfiguredForwarder(configuration);
        if (forwarder != null)
        {
            services.AddSingleton(forwarder);
        }

        return services;
    }

    /// <summary>
    /// Registers the connection manager and every supported device protocol that is both allowed by
    /// the optional Protocols:Enable allow-list and has a configured (and positive) port.
    ///
    /// For each protocol:
    ///   1. Named ProtocolOptions are registered via IOptionsMonitor (services.Configure), binding
    ///      the "Protocols:{name}" section so the monitor is available to any consumer.
    ///   2. The protocol singleton is registered with a factory that calls
    ///      IOptionsMonitor.Get(name) to materialise the concrete ProtocolOptions instance and
    ///      passes it to ActivatorUtilities.CreateInstance — so constructors receive ProtocolOptions
    ///      directly rather than IOptionsMonitor.
    /// </summary>
    /// <summary>
    /// Registers the geocoder selected by Geocoder:Type. Only "nominatim" is supported;
    /// if Geocoder:Type is unset or unknown, no geocoder is registered and GeocoderHandler is skipped.
    /// </summary>
    public static IServiceCollection AddTraccarGeocoder(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.Equals(configuration[ConfigKeys.Geocoder.Type], "nominatim", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IGeocoderService>(sp =>
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Traccar/6.0 (https://www.traccar.org)");
                client.Timeout = TimeSpan.FromSeconds(30);
                var config = sp.GetRequiredService<IConfiguration>();
                return new NominatimGeocoder(client, config);
            });
        }

        return services;
    }

    public static IServiceCollection AddTraccarProtocols(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ConnectionManager>();
        services.AddSingleton<PositionCache>();
        services.AddSingleton<DeviceLookupService>();
        services.AddSingleton<VideoStreamManager>();
        services.AddTraccarPositionForwarding(configuration);
        services.AddTraccarGeocoder(configuration);

        HashSet<string>? enabledProtocols = null;
        var enableList = configuration[ConfigKeys.Protocols.Enable];
        if (!string.IsNullOrEmpty(enableList))
        {
            enabledProtocols = enableList.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var protocolTypes = typeof(BaseProtocol).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsSubclassOf(typeof(BaseProtocol)));

        foreach (var type in protocolTypes)
        {
            var name = BaseProtocol.NameFromType(type);
            if (enabledProtocols != null && !enabledProtocols.Contains(name))
            {
                continue;
            }
            if (configuration.GetValue<int?>($"{ConfigKeys.Protocols.SectionPrefix}:{name}:{ConfigKeys.Protocols.Port}") is not > 0)
            {
                continue;
            }

            // Register named options — makes IOptionsMonitor<ProtocolOptions> available to any
            // consumer that wants to inspect or monitor protocol configuration.
            var section = configuration.GetSection($"{ConfigKeys.Protocols.SectionPrefix}:{name}");
            services.Configure<ProtocolOptions>(name, opts =>
            {
                opts.Name = name;
                opts.Port = section.GetValue<int>(ConfigKeys.Protocols.Port);
                opts.Timeout = section.GetValue(ConfigKeys.Protocols.Timeout, ProtocolOptions.DefaultTimeout);
            });

            // Register the protocol as a singleton via factory: resolve the named ProtocolOptions
            // from IOptionsMonitor and inject the concrete instance into the protocol constructor
            // via ActivatorUtilities, so protocols depend on ProtocolOptions not on the monitor.
            var capturedName = name;
            var capturedType = type;
            services.AddSingleton(typeof(BaseProtocol), sp =>
            {
                var opts = sp.GetRequiredService<IOptionsMonitor<ProtocolOptions>>().Get(capturedName);
                return ActivatorUtilities.CreateInstance(sp, capturedType, opts);
            });
        }

        services.AddSingleton<ServerManager>();

        return services;
    }
}
