using RabbitMQ.Client;

namespace NotificationService
{
    public interface IQueueConnectionProvider : IAsyncDisposable
    {
        ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
    }
}