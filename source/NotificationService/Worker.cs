using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace NotificationService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private ConnectionFactory? _factory;
        private IConnection? _connection;
        private IModel? _channel;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting worker at: {time} {platform}", DateTimeOffset.Now, Environment.OSVersion.Platform);

            var queueName = _configuration.GetValue<string>("MessageBroker:queueName");
            var hostName = _configuration.GetValue<string>("MessageBroker:address");

            var port = _configuration.GetValue<int>("MessageBroker:port");


            _logger.LogInformation($"Connecting to {hostName}:{port}/{queueName}", hostName , port, queueName);

            _factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                DispatchConsumersAsync = true,
            };

           _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.Close();
            _connection?.Close();
            _logger.LogInformation("RabbitMQ connection is closed.");

            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, deliverEventArgs) =>
            {
                var body = deliverEventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    await PushMessageAsync(message, cancellationToken);
                    _channel?.BasicAck(deliverEventArgs.DeliveryTag, false);
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

            var queueName = _configuration.GetValue<string>("MessageBroker:queueName");

            _channel?.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

            await Task.CompletedTask;
        }

        private async Task PushMessageAsync(string message, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                ["token"] = _configuration.GetValue<string>("appToken"),
                ["user"] = _configuration.GetValue<string>("userKey"),
                ["message"] = message
            };

            var pushEndpoint = _configuration.GetValue<string>("PushoverConfiguration:endpoint");
            var uri = QueryHelpers.AddQueryString(pushEndpoint, parameters);


            _logger.LogInformation($"Attempting to send the message to {pushEndpoint}");
            
            using var client = _httpClientFactory.CreateClient();
            HttpResponseMessage response = await client.PostAsync(uri, null, cancellationToken);

        }

    }
}