using Polly.Registry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace NotificationService
{
    public class RabbitMqConnectionProvider : IAsyncDisposable
    {
        private Task<IConnection>? _connectionTask;

        private readonly ILogger<RabbitMqConnectionProvider> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConnectionFactory _factory;
        private readonly ResiliencePipelineProvider<string> _pipelineProvider;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public RabbitMqConnectionProvider(ILogger<RabbitMqConnectionProvider> logger, IConfiguration configuration, ResiliencePipelineProvider<string> pipelineProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _pipelineProvider = pipelineProvider;

            _factory = new ConnectionFactory
            {
                HostName = _configuration.GetValue<string>("MessageBroker:address"),
                Port = _configuration.GetValue<int>("MessageBroker:port"),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                ClientProvidedName = "WorkerService",
            };
        }

        public async ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_connectionTask == null || !_connectionTask.Result.IsOpen)
                {
                    _connectionTask = CreateConnectionWithRetryAsync(cancellationToken);
                }
                return await _connectionTask;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<IConnection> CreateConnectionWithRetryAsync(CancellationToken cancellationToken)
        {
            var retryPolicy = _pipelineProvider.GetPipeline("BrokerConnectionPipeline");
            IConnection? connection = null;

            await retryPolicy.ExecuteAsync(async _ =>
            {
                connection = await _factory.CreateConnectionAsync(cancellationToken);
            }, cancellationToken);

            if (connection == null)
            {
                throw new InvalidOperationException("Failed to establish a connection to RabbitMQ.");
            }

            connection.ConnectionShutdownAsync += (_, e) =>
            {
                _logger.LogError($"Connection to RabbitMQ broker {connection.Endpoint.HostName}:{connection.RemotePort} has been shutdown. Reason: {e.ReplyText}");
                return Task.CompletedTask;
            };

            connection.CallbackExceptionAsync += (_, e) =>
            {
                _logger.LogError($"Callback exception in RabbitMQ connection: {e.Exception.Message}");
                return Task.CompletedTask;
            };

            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            if (_connectionTask != null)
            {
                try
                {
                    var connection = await _connectionTask;
                    await connection.DisposeAsync();
                }
                catch(BrokerUnreachableException ex)
                {
                    _logger.LogWarning($"{ex.GetType()} exception occurred while disposing RabbitMQ connection. The connection may have already been closed.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception occurred while disposing RabbitMQ connection.");
                }
            }
        }
    }
}
