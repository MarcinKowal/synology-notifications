using Polly;
using RabbitMQ.Client.Exceptions;
using Serilog;

using Serilog.Enrichers.Sensitive;

namespace NotificationService
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var logger = new LoggerConfiguration()
            .Enrich
            .WithSensitiveDataMasking(options => options.MaskProperties.Add(new MaskProperty
            {
                Name = "token",
            }))
            .WriteTo.Console()
            .CreateLogger();

            Log.Logger = logger;

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
               .ConfigureLogging(logging =>
                {
                   // logging.ClearProviders();
                    logging.AddSerilog(logger, dispose: true);
                })
                .Build();
            host.Run();
        }
    }
}