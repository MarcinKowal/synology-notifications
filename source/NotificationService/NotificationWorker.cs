using Microsoft.Extensions.Options;
using NotificationService.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace NotificationService
{
    internal class NotificationWorker : BackgroundService
    {
        private readonly ILogger<NotificationWorker> _logger;
        private readonly IQueueConnectionProvider _connectionProvider;
        private readonly PushoverService _pushoverService;
        private readonly string _queueName;
        private IConnection? _connection;
        private IConnection Connection => _connection ?? throw new InvalidOperationException("Connection is not established.");

        public NotificationWorker(ILogger<NotificationWorker> logger, IOptions<MessageBrokerConfig> configuration, IQueueConnectionProvider connectionProvider, PushoverService pushoverService)
        {
            _logger = logger;
            _connectionProvider = connectionProvider;
            _pushoverService = pushoverService;

            _queueName = configuration.Value.QueueName ?? throw new InvalidOperationException("Queue name is not configured.");

        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting notification worker at: {time} {platform}", DateTimeOffset.UtcNow, Environment.OSVersion.Platform);

            _connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            _logger.LogInformation($"Connecting to {_connection.Endpoint.HostName}:{_connection.Endpoint.Port}/{_queueName}");

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ConsumeMessageAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Consumer crashed");

                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        cancellationToken);
                }
            }
        }

        private async Task ConsumeMessageAsync(CancellationToken cancellationToken)
        {

            await using var channel = await Connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // durable queue survives broker restart
            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                 arguments: new Dictionary<string, object?>
                 {
                     ["x-queue-type"] = "quorum",
                     ["x-delivery-limit"] = 5,
                     ["x-dead-letter-exchange"] = "",
                     ["x-dead-letter-routing-key"] = "notifications.dlq"
                 },
                cancellationToken: cancellationToken);

            // Prevent consumer overload
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                NotificationRequest? message = null;
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    message = JsonSerializer.Deserialize<NotificationRequest>(json) ?? throw new InvalidOperationException("Invalid message");

                    await ProcessMessageAsync(message, cancellationToken);

                    await channel.BasicAckAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        cancellationToken: cancellationToken);

                    _logger.LogInformation(
                        "Message ACK: {MessageId}",
                        message.Id);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid payload");

                    await channel.BasicRejectAsync(
                        ea.DeliveryTag,
                        requeue: false);
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, "Message {MessageId} processing failed", message?.Id);

                    await channel.BasicNackAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: cancellationToken);
                }
            };

            await channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);

            _logger.LogInformation("NotificationWorker started");

            // KEEP CONSUMER ALIVE
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        private async Task ProcessMessageAsync(NotificationRequest notificationMessage, CancellationToken cancellationToken)
        {
            await _pushoverService.PushMessageAsync(notificationMessage.Message, cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping notification worker.");

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
