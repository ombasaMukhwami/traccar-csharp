using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Traccar.Protocols.Forward;
using Traccar.Protocols.Media;
using Traccar.Protocols.Session;

namespace Traccar.Protocols;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the position forwarder selected by Forward:Type, mirroring Java's
    /// MainModule.providePositionForwarder - only one forwarder is active at a time, and none is
    /// registered (PositionForwardingHandler becomes a no-op) if Forward:Type is unset.
    /// </summary>
    public static IServiceCollection AddTraccarPositionForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        switch (configuration[ConfigKeys.Forward.Type])
        {
            case ConfigKeys.Forward.TypeKafka:
                services.AddSingleton<IPositionForwarder>(_ => new PositionForwarderKafka(configuration));
                break;
            case ConfigKeys.Forward.TypeRabbitMq:
                services.AddSingleton<IPositionForwarder>(_ => new PositionForwarderRabbitMq(configuration));
                break;
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
    public static IServiceCollection AddTraccarProtocols(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ConnectionManager>();
        services.AddSingleton<DeviceLookupService>();
        services.AddSingleton<VideoStreamManager>();
        services.AddTraccarPositionForwarding(configuration);

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
