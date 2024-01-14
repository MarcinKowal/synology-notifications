namespace NotificationService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
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