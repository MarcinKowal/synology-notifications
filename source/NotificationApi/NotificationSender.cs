using System.Text;
using Polly.Registry;
using RabbitMQ.Client;

namespace NotificationApi;

public class NotificationSender
{
    private readonly ILogger<NotificationSender> _logger;
    private readonly IConfiguration _configuration;
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public NotificationSender(ILogger<NotificationSender> logger, IConfiguration configuration, RabbitMqConnectionProvider connectionProvider,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionProvider = connectionProvider;
        _pipelineProvider = pipelineProvider;
    }

    public async ValueTask SendMessageAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var queueName = _configuration.GetValue<string>("MessageBroker:queueName");
        var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
        var pipeline = _pipelineProvider.GetPipeline("MessagePublishPipeline");

        await pipeline.ExecuteAsync(async _ =>
        {
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false, 
                cancellationToken: cancellationToken);

            var message = request.Message;
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>
                {
                    { "x-message-type", "Notification" },
                    { "x-message-id", Guid.NewGuid().ToString() }
                },
                ContentEncoding = "utf-8",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Type = "NotificationRequest"
            };

            await channel.BasicPublishAsync(exchange: string.Empty,
                routingKey: queueName, mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogInformation($"Successfully sent message to broker {connection.Endpoint.HostName}:{connection.RemotePort}");

            await channel.CloseAsync(cancellationToken);
        }, cancellationToken);
    }
}