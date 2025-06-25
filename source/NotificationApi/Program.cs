using Microsoft.AspNetCore.Mvc;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace NotificationApi;

public class Program
{
    public static void Main(string[] args)
    {

        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenAnyIP(5671, listenOptions =>
            {
                listenOptions.UseConnectionLogging();
            });
        });


        builder.Services.AddResiliencePipeline("MessagePublishPipeline", static (builder, context) =>
        {
            builder. AddRetry(new()
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = args => new ValueTask<bool>(args.Outcome.Exception is not null),
                OnRetry = args =>
                {
                    var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning($"Retrying message publish. Attempt {args.AttemptNumber}. Exception: {args.Outcome.Exception?.Message}");
                    return default;
                }
            });
        });

        builder.Services.AddResiliencePipeline("BrokerConnectionPipeline", static (builder, context) =>
        {
            builder.AddRetry(new()
            {
                MaxRetryAttempts =2,
                BackoffType = DelayBackoffType.Linear,
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

        builder.Services.AddLogging();
        builder.Services.AddSingleton<RabbitMqConnectionProvider>();
        builder.Services.AddTransient<NotificationSender>();
        var app = builder.Build();

        app.MapGet("/", () => "Running!!");
        app.MapPost("/api/Notifications", async (NotificationSender sender, [FromBody] NotificationRequest request, CancellationToken cancellationToken) =>
            await sender.SendMessageAsync(request, cancellationToken))
            .WithName("SendNotification");

        app.Run();
    }
}