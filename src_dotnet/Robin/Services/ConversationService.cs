using System.Text.Json;
using Android.Content;
using Android.Util;
using Robin.Models;

namespace Robin.Services;

public class ConversationService
{
    private readonly List<Message> _messages = new();
    private readonly ISharedPreferences? _preferences;
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
            Content = content,
            OriginalRecognizedText = content,
            DisplayState = MessageDisplayState.RawRecognized
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

    /// <summary>
    /// 指定インデックスのメッセージに意味検証結果を反映
    /// </summary>
    public void UpdateMessageWithSemanticValidation(int messageIndex, SemanticValidationResult validationResult)
    {
        if (messageIndex >= 0 && messageIndex < _messages.Count)
        {
            var message = _messages[messageIndex];
            message.SemanticValidation = validationResult;
            message.DisplayState = validationResult.IsSemanticValid ?
                MessageDisplayState.SemanticValidated :
                MessageDisplayState.RawRecognized;

            // 意味が通じた場合は Content も更新
            if (validationResult.IsSemanticValid && !string.IsNullOrEmpty(validationResult.CorrectedText))
            {
                message.Content = validationResult.CorrectedText;
            }

            SaveMessagesToStorage();
        }
    }

    /// <summary>
    /// 最後のユーザーメッセージを取得
    /// </summary>
    public Message? GetLastUserMessage()
    {
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            if (_messages[i].IsUser)
                return _messages[i];
        }
        return null;
    }

    /// <summary>
    /// 最後のユーザーメッセージのインデックスを取得
    /// </summary>
    public int GetLastUserMessageIndex()
    {
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            if (_messages[i].IsUser)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// 指定インデックスのメッセージを削除
    /// </summary>
    public void DeleteMessage(int index)
    {
        if (index >= 0 && index < _messages.Count)
        {
            _messages.RemoveAt(index);
            SaveMessagesToStorage();
            Log.Info("ConversationService", $"メッセージを削除しました (インデックス: {index})");
        }
    }

    public void ClearHistory()
    {
        _messages.Clear();
        ClearMessagesFromStorage();
    }

    public int MessageCount => _messages.Count;

    /// <summary>
    /// 最後のユーザーメッセージが意味判定失敗している場合、そのメッセージを取得
    /// </summary>
    public Message? GetLastSemanticInvalidMessage()
    {
        var lastUserMessage = GetLastUserMessage();
        if (lastUserMessage != null &&
            lastUserMessage.SemanticValidation != null &&
            !lastUserMessage.SemanticValidation.IsSemanticValid)
        {
            return lastUserMessage;
        }
        return null;
    }

    /// <summary>
    /// 最後の意味判定失敗メッセージと新しいテキストを結合してメッセージを更新
    /// 失敗メッセージがない場合は新規メッセージを追加
    /// </summary>
    /// <returns>結合されたテキスト</returns>
    public string MergeWithLastInvalidMessage(string newText)
    {
        var lastInvalidMsg = GetLastSemanticInvalidMessage();
        if (lastInvalidMsg != null)
        {
            // 前のメッセージと結合
            var mergedText = $"{lastInvalidMsg.Content} {newText}";

            // 前のメッセージの Content を更新
            lastInvalidMsg.Content = mergedText;
            lastInvalidMsg.OriginalRecognizedText = mergedText;
            lastInvalidMsg.SemanticValidation = null; // 再判定のためクリア
            lastInvalidMsg.DisplayState = MessageDisplayState.RawRecognized;

            SaveMessagesToStorage();

            // UIに更新を通知
            var index = GetLastUserMessageIndex();
            if (index >= 0)
            {
                MessageAdded?.Invoke(this, lastInvalidMsg);
            }

            return mergedText;
        }

        // 失敗メッセージがない場合は新規追加して新しいテキストを返す
        return newText;
    }

    /// <summary>
    /// 結合に使われた古いメッセージを削除して、統合されたメッセージを1つ作成
    /// </summary>
    /// <param name="mergedMessageIndex">統合されたメッセージのインデックス</param>
    /// <returns>削除されたメッセージの数</returns>
    public int ConsolidateMergedMessages(int mergedMessageIndex)
    {
        if (mergedMessageIndex < 0 || mergedMessageIndex >= _messages.Count)
        {
            return 0;
        }

        // 統合されたメッセージより後のユーザーメッセージを収集
        var messagesToDelete = new List<int>();
        for (int i = mergedMessageIndex + 1; i < _messages.Count; i++)
        {
            if (_messages[i].IsUser)
            {
                messagesToDelete.Add(i);
            }
        }

        // 削除するメッセージがある場合
        if (messagesToDelete.Count > 0)
        {
            // 後ろから削除（インデックスがずれないように）
            for (int i = messagesToDelete.Count - 1; i >= 0; i--)
            {
                _messages.RemoveAt(messagesToDelete[i]);
            }

            SaveMessagesToStorage();
            Log.Info("ConversationService", $"結合メッセージを統合: {messagesToDelete.Count}個のメッセージを削除");

            return messagesToDelete.Count;
        }

        return 0;
    }

    /// <summary>
    /// チャット履歴をローカルストレージに保存
    /// </summary>
    private void SaveMessagesToStorage()
    {
        try
        {
            var json = JsonSerializer.Serialize(_messages);
            var editor = _preferences?.Edit();
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
            var json = _preferences?.GetString(MessagesKey, null);
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
            var editor = _preferences?.Edit();
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
