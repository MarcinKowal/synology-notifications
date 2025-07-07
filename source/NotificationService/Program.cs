using Polly;
using RabbitMQ.Client.Exceptions;

namespace NotificationService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((context, services) =>
                {
                    services.AddResiliencePipeline("BrokerConnectionPipeline", static (builder, context) =>
                    {
                        builder.AddRetry(new()
                        {
                            MaxRetryAttempts = 5,
                            BackoffType = DelayBackoffType.Exponential,
                            Delay = TimeSpan.FromSeconds(2),
                            ShouldHandle = args => new ValueTask<bool>(args.Outcome.Exception is BrokerUnreachableException),
                            OnRetry = args =>
                            {
                                var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
                                logger.LogWarning($"Retrying connection to broker. Attempt {args.AttemptNumber}. Exception: {args.Outcome.Exception?.Message}");
                                return default;
                            }
                        });
                    });

                    services.AddSingleton<RabbitMqConnectionProvider>();
                    services.AddHttpClient<PushoverService>();
                    services.AddHostedService<Worker>();
                })
                .Build();


            host.Run();
        }
    }
}