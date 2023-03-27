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

            var hostName = Environment.GetEnvironmentVariable("RABBIT_HOSTNAME");

            var queueName = _configuration.GetRequiredSection("MessageBroker").GetValue<string>("queueName");

            _factory = new ConnectionFactory
            {
                HostName = _configuration.GetRequiredSection("MessageBroker").GetValue<string>("address") ,
                Port = _configuration.GetRequiredSection("MessageBroker").GetValue<int>("port"), 
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    await PushMessageAsync(message, stoppingToken);
                    _channel?.BasicAck(ea.DeliveryTag, false);
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

            _channel?.BasicConsume(queue: _configuration.GetRequiredSection("MessageBroker").GetValue<string>("queueName"), autoAck: false, consumer: consumer);

            await Task.CompletedTask;
        }

        private async Task PushMessageAsync(string message, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                ["token"] = _configuration.GetRequiredSection("PushoverConfiguration").GetValue<string>("appToken"),
                ["user"] = _configuration.GetRequiredSection("PushoverConfiguration").GetValue<string>("userKey"),
                ["message"] = message
            };

            var pushEndpoint = _configuration.GetRequiredSection("PushoverConfiguration").GetValue<string>("endpoint");
            var uri = QueryHelpers.AddQueryString(pushEndpoint, parameters);
            using var client = _httpClientFactory.CreateClient();
            HttpResponseMessage response = await client.PostAsync(uri, null, cancellationToken);

        }

        private async Task SendMessage(CancellationToken stoppingToken, IModel channel)
        {
            var queueName = _configuration.GetRequiredSection("MessageBroker").GetValue<string>("queueName");
            var message = "Hello World!";
            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(exchange: string.Empty,
                routingKey: queueName,
                basicProperties: null,
                body: body);

            await Task.Delay(1000, stoppingToken);
        }
    }
}