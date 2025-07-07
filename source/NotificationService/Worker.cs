using System.Text;
using Polly.Registry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace NotificationService
{
    internal class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        
        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly PushoverService _pushoverService;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, RabbitMqConnectionProvider connectionProvider,
        ResiliencePipelineProvider<string> pipelineProvider, PushoverService pushoverService)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            _pushoverService = pushoverService;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting notification worker at: {time} {platform}", DateTimeOffset.Now, Environment.OSVersion.Platform);

            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping notification worker.");
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var queueName = _configuration.GetValue<string>("MessageBroker:queueName");
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            _logger.LogInformation($"Connecting to {connection.Endpoint.HostName}:{connection.Endpoint.Port}/{queueName}");
            
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, deliverEventArgs) =>
            {
                var body = deliverEventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    await _pushoverService.PushMessageAsync(message, cancellationToken);
                    await channel.BasicAckAsync(deliverEventArgs.DeliveryTag, false);
                }
                catch (AlreadyClosedException)
                {
                    _logger.LogInformation("RabbitMQ is closed!");
                }
                catch (Exception e)
                {
                    _logger.LogError(default, e, e.Message);
                }
            };

            await consumer.Channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
            
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}