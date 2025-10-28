using Android.Content;
using Android.Util;
using Robin.Models;
using System.Text.Json;

namespace Robin.Services;

public class ConversationService
{
    private readonly List<Message> _messages = new();
    private readonly ISharedPreferences _preferences;
    private const string PreferencesName = "robin_chat";
    private const string MessagesKey = "conversation_messages";

    public event EventHandler<Message>? MessageAdded;

    public ConversationService(Context? context = null)
    {
        if (context != null)
        {
            _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
                ?? throw new InvalidOperationException("Failed to initialize SharedPreferences");
            LoadMessagesFromStorage();
        }
    }

    public void AddUserMessage(string content)
    {
        var message = new Message
        {
            Role = MessageRole.User,
            Content = content
        };

        _messages.Add(message);
        SaveMessagesToStorage();
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
        SaveMessagesToStorage();
        MessageAdded?.Invoke(this, message);
    }

    public List<Message> GetMessages() => new(_messages);

    public void ClearHistory()
    {
        _messages.Clear();
        ClearMessagesFromStorage();
    }

    public int MessageCount => _messages.Count;

    /// <summary>
    /// チャット履歴をローカルストレージに保存
    /// </summary>
    private void SaveMessagesToStorage()
    {
        try
        {
            var json = JsonSerializer.Serialize(_messages);
            var editor = _preferences.Edit();
            if (editor != null)
            {
                editor.PutString(MessagesKey, json);
                editor.Commit();
                Log.Info("ConversationService", $"チャット履歴を保存しました ({_messages.Count}件)");
            }
        }
        catch (Exception ex)
        {
            Log.Error("ConversationService", $"チャット履歴の保存に失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// ローカルストレージからチャット履歴を読み込む
    /// </summary>
    private void LoadMessagesFromStorage()
    {
        try
        {
            var json = _preferences.GetString(MessagesKey, null);
            if (!string.IsNullOrEmpty(json))
            {
                var messages = JsonSerializer.Deserialize<List<Message>>(json);
                if (messages != null)
                {
                    _messages.Clear();
                    _messages.AddRange(messages);
                    Log.Info("ConversationService", $"チャット履歴を読み込みました ({_messages.Count}件)");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("ConversationService", $"チャット履歴の読み込みに失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// ローカルストレージからチャット履歴を削除
    /// </summary>
    private void ClearMessagesFromStorage()
    {
        try
        {
            var editor = _preferences.Edit();
            if (editor != null)
            {
                editor.Remove(MessagesKey);
                editor.Commit();
                Log.Info("ConversationService", "チャット履歴をクリアしました");
            }
        }
        catch (Exception ex)
        {
            Log.Error("ConversationService", $"チャット履歴のクリアに失敗: {ex.Message}");
        }
    }
}
