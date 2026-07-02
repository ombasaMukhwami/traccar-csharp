using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace Traccar.Protocols.Forward;

/// <summary>Mirrors Java's forward.PositionForwarderKafka.</summary>
public sealed class PositionForwarderKafka : IPositionForwarder, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public PositionForwarderKafka(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration[ConfigKeys.Forward.Url],
            Acks = Acks.All,
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
        _topic = configuration[ConfigKeys.Forward.Topic] ?? ConfigKeys.Forward.DefaultTopic;
    }

    public void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler)
    {
        try
        {
            var key = data.Position.DeviceId.ToString();
            var value = JsonSerializer.Serialize(data, JsonOptions);
            _producer.Produce(_topic, new Message<string, string> { Key = key, Value = value }, report =>
            {
                resultHandler(report.Error.IsError == false, report.Error.IsError ? new KafkaException(report.Error) : null);
            });
        }
        catch (Exception e)
        {
            resultHandler(false, e);
        }
    }

    public void Dispose() => _producer.Dispose();
}
