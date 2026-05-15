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

            var response = await _httpClient.PostAsync(uri, null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Message pushed successfully to Pushover.");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to push message to Pushover. Status Code: {statusCode}, Response: {response}", response.StatusCode, errorContent);
            }
        }
    }
}
