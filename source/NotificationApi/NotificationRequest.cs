namespace NotificationApi;

public record NotificationRequest
{
    public string Message
    {
        get;
        init;
    }
    
}