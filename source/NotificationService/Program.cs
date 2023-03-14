namespace NotificationService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    if (!context.HostingEnvironment.IsProduction())
                    {
                        return;
                    }

                    var configPath = Path.Combine(context.HostingEnvironment.ContentRootPath, "config", "appsettings.json");
                    builder.AddJsonFile(configPath);
                })
                .ConfigureServices((context, services) =>
                {
                    
                    services.AddHostedService<Worker>();
                    services.AddHttpClient();
                })
                .Build();


            host.Run();
        }
    }
}