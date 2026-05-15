namespace NotificationApi;

public sealed record NotificationRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Message { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}