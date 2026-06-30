using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        switch (configuration["Forward:Type"])
        {
            case "kafka":
                services.AddSingleton<IPositionForwarder>(_ => new PositionForwarderKafka(configuration));
                break;
            case "rabbitmq":
                services.AddSingleton<IPositionForwarder>(_ => new PositionForwarderRabbitMq(configuration));
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers the connection manager and every supported device protocol that is both allowed by
    /// the optional Protocols:Enable allow-list and has a configured (and positive) port. Mirrors
    /// Java's ServerManager, which reflection-scans for BaseProtocol subclasses (ClassScanner.
    /// findSubclasses) and only calls injector.getInstance(protocolClass) - constructing the protocol
    /// and eagerly binding its listeners - under the same two conditions; protocols that fail either
    /// check are never instantiated, so a missing config section never crashes the host.
    /// </summary>
    public static IServiceCollection AddTraccarProtocols(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ConnectionManager>();
        services.AddSingleton<DeviceLookupService>();
        services.AddSingleton<VideoStreamManager>();
        services.AddTraccarPositionForwarding(configuration);

        HashSet<string>? enabledProtocols = null;
        var enableList = configuration["Protocols:Enable"];
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
            if (configuration.GetValue<int?>($"Protocols:{name}:Port") is not > 0)
            {
                continue;
            }
            services.AddSingleton(typeof(BaseProtocol), type);
        }

        services.AddSingleton<ServerManager>();

        return services;
    }
}
