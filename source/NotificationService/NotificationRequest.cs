namespace NotificationService;
public sealed record NotificationRequest(Guid Id, string Message, DateTime CreatedAtUtc);
