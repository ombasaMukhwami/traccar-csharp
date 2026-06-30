using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace Traccar.Protocols.Forward;

/// <summary>Mirrors Java's forward.PositionForwarderKafka.</summary>
public sealed class PositionForwarderKafka : IPositionForwarder, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IProducer<string, string> producer;
    private readonly string topic;

    public PositionForwarderKafka(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Forward:Url"],
            Acks = Acks.All,
        };
        producer = new ProducerBuilder<string, string>(config).Build();
        topic = configuration["Forward:Topic"] ?? "positions";
    }

    public void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler)
    {
        try
        {
            var key = data.Position.DeviceId.ToString();
            var value = JsonSerializer.Serialize(data, JsonOptions);
            producer.Produce(topic, new Message<string, string> { Key = key, Value = value }, report =>
            {
                resultHandler(report.Error.IsError == false, report.Error.IsError ? new KafkaException(report.Error) : null);
            });
        }
        catch (Exception e)
        {
            resultHandler(false, e);
        }
    }

    public void Dispose() => producer.Dispose();
}
