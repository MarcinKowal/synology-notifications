using System.Text;
using RabbitMQ.Client;

namespace NotificationApi;

public class NotificationSender
{
    private readonly ILogger<NotificationSender> _logger;
    private readonly IConfiguration _configuration;

    public NotificationSender(ILogger<NotificationSender> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public ValueTask SendMessageAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var queueName = _configuration.GetValue<string>("MessageBroker:queueName");
        var hostName = _configuration.GetValue<string>("MessageBroker:address");
        var port = _configuration.GetValue<int>("MessageBroker:port");
        
        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
        };


        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var message = request.Message;
        var body = Encoding.UTF8.GetBytes(message);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(exchange: string.Empty,
            routingKey: queueName,
            basicProperties: properties,
            body: body);


        _logger.LogInformation($"Succesfully sent message to broker {hostName}:{port}");

        return new ValueTask();
    }
}