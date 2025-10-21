using Robin.Models;

namespace Robin.Services;

public class ConversationService
{
    private readonly List<Message> _messages = new();

    public event EventHandler<Message>? MessageAdded;

    public void AddUserMessage(string content)
    {
        var message = new Message
        {
            Role = MessageRole.User,
            Content = content
        };

        _messages.Add(message);
        MessageAdded?.Invoke(this, message);
    }

    public void AddAssistantMessage(string content)
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Content = content
        };

        _messages.Add(message);
        MessageAdded?.Invoke(this, message);
    }

    public List<Message> GetMessages() => new(_messages);

    public void ClearHistory()
    {
        _messages.Clear();
    }

    public int MessageCount => _messages.Count;
}
