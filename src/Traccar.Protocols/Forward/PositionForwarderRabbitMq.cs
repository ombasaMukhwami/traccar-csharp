using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Traccar.Protocols.Forward;

/// <summary>Mirrors Java's forward.PositionForwarderAmqp / forward.AmqpClient.</summary>
public sealed class PositionForwarderRabbitMq : IPositionForwarder, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _exchange;
    private readonly string _routingKey;
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public PositionForwarderRabbitMq(IConfiguration configuration)
    {
        _exchange = configuration[ConfigKeys.Forward.Exchange] ?? ConfigKeys.Forward.DefaultExchange;
        _routingKey = configuration[ConfigKeys.Forward.Topic] ?? ConfigKeys.Forward.DefaultTopic;

        var factory = new ConnectionFactory { Uri = new Uri(configuration[ConfigKeys.Forward.Url]!) };
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(_exchange, ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
    }

    public void Forward(PositionForwardData data, Action<bool, Exception?> resultHandler)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOptions));
            var properties = new BasicProperties { Persistent = true };
            _channel.BasicPublishAsync(_exchange, _routingKey, mandatory: false, properties, body)
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
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}
