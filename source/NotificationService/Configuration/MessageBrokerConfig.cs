namespace NotificationService.Configuration
{
    internal record MessageBrokerConfig
    {
        public required string Address { get; init; }
        public required int Port { get; init; }
        public required string QueueName { get; init; }
    }
}
