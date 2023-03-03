using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService
{
    public class Worker : BackgroundService
    {
        private const string PushoverAppToken = "afcf2tcn3zi9o9pj7qfygetme7b5cp";
        private const string PushoverUserKey = "uddt8z25yyjkqjcj5s6x6awcgputtt";
       
        private readonly ILogger<Worker> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = MessageQueueConfiguration.BrokerAddress,
                Port =MessageQueueConfiguration.BrokerPort,
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

            channel.BasicConsume(queue: MessageQueueConfiguration.QueueName,
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
                ["token"] = PushoverAppToken,
                ["user"] = PushoverUserKey,
                ["message"] = message
            };

            var uri = QueryHelpers.AddQueryString("https://api.pushover.net/1/messages.json", parameters);
            using var client = _httpClientFactory.CreateClient();
            HttpResponseMessage response = await client.PostAsync(uri, null, cancellationToken);

        }

        private static async Task SendMessage(CancellationToken stoppingToken, IModel channel)
        {

            var message = "Hello World!";
            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(exchange: string.Empty,
                routingKey: MessageQueueConfiguration.QueueName,
                basicProperties: null,
                body: body);

            await Task.Delay(1000, stoppingToken);
        }
    }
}