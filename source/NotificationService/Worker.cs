using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration.GetRequiredSection("MessageBroker").GetValue<string>("address"),
                Port = _configuration.GetRequiredSection("MessageBroker").GetValue<int>("port"),
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received {message}");

                await PushMessageAsync(message, stoppingToken);
            };

            channel.BasicConsume(queue: _configuration.GetRequiredSection("MessageBroker").GetValue<string>("queueName"),
                autoAck: true,
                consumer: consumer);


            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time} {platform}", DateTimeOffset.Now, Environment.OSVersion.Platform);


              //  await SendMessage(stoppingToken, channel);
            }
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