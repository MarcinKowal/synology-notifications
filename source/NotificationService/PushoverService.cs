using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;

namespace NotificationService
{
    internal class PushoverService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PushoverService> _logger;
        private readonly IOptions<PushoverConfig> _configuration;

        public PushoverService(HttpClient httpClient, ILogger<PushoverService> logger, IOptions<PushoverConfig> configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task PushMessageAsync(string message, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                ["token"] = _configuration.Value.AppToken,
                ["user"] = _configuration.Value.UserKey,
                ["message"] = message
            };


            var pushEndpoint = _configuration.Value.Endpoint;
            var uri = QueryHelpers.AddQueryString(pushEndpoint, parameters!);

            var response = await _httpClient.PostAsync(uri, null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send push notification. Status Code: {statusCode}, Response: {response}", response.StatusCode, errorContent);
            }

            response.EnsureSuccessStatusCode();
        }
    }
}
