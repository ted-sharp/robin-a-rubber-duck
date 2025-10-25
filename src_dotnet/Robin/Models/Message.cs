namespace Robin.Models;

public enum MessageRole
{
    User,
    Assistant
}

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public bool IsUser => Role == MessageRole.User;
    public bool IsAssistant => Role == MessageRole.Assistant;

    public string FormattedTime => Timestamp.ToString("HH:mm");
}
