using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Traccar.Protocols.Forward;

/// <summary>Mirrors Java's forward.PositionForwarderAmqp / forward.AmqpClient.</summary>
public sealed class PositionForwarderRabbitMq : IPositionForwarder, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string exchange;
    private readonly string routingKey;
    private readonly IConnection connection;
    private readonly IChannel channel;

    public PositionForwarderRabbitMq(IConfiguration configuration)
    {
        exchange = configuration["Forward:Exchange"] ?? "traccar";
        routingKey = configuration["Forward:Topic"] ?? "positions";

        var factory = new ConnectionFactory { Uri = new Uri(configuration["Forward:Url"]!) };
        connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
        channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
    }

    public void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOptions));
            var properties = new BasicProperties { Persistent = true };
            channel.BasicPublishAsync(exchange, routingKey, mandatory: false, properties, body)
                .AsTask()
                .ContinueWith(task => resultHandler(!task.IsFaulted, task.Exception));
        }
        catch (Exception e)
        {
            resultHandler(false, e);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await channel.CloseAsync();
        await connection.CloseAsync();
    }
}
