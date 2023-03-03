namespace NotificationService;

internal static class MessageQueueConfiguration
{
    public const string BrokerAddress = "172.17.0.2";

    public const string QueueName = "notifications";
    public const int BrokerPort = 5672;
}