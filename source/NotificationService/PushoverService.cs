using Microsoft.AspNetCore.WebUtilities;

namespace NotificationService
{
    internal class PushoverService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PushoverService> _logger;
        private readonly IConfiguration _configuration;

        public PushoverService(HttpClient httpClient, ILogger<PushoverService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;   
            _logger = logger;
            _configuration = configuration;
        }

        public async Task PushMessageAsync(string message, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                ["token"] = _configuration.GetValue<string>("appToken"),
                ["user"] = _configuration.GetValue<string>("userKey"),
                ["message"] = message
            };

            var pushEndpoint = _configuration.GetValue<string>("PushoverConfiguration:endpoint");
            var uri = QueryHelpers.AddQueryString(pushEndpoint, parameters);

            await _httpClient.PostAsync(uri, null, cancellationToken);
        }
    }
}
