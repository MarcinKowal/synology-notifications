using Microsoft.AspNetCore.Mvc;

namespace NotificationApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<NotificationSender>();
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();


        app.MapPost("/Notifications", async (NotificationSender sender, [FromBody] NotificationRequest request,  CancellationToken cancellationToken) =>
            await sender.SendMessageAsync(request, cancellationToken))
            .WithName("SendNotification");

        app.Run();
    }
}