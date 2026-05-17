using System.ComponentModel.DataAnnotations;

namespace NotificationService.Configuration
{
    internal record PushoverConfig
    {
        [Required]
        public required string AppToken { get; init; }
        [Required]
        public required string UserKey { get; init; }
        [Required]
        public required string Endpoint { get; init; }
    }
}
