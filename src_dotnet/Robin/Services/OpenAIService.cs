using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Android.Util;
using Robin.Models;

namespace Robin.Services;

public class OpenAIService
{
    private const string OpenAIBaseAddress = "https://api.openai.com/v1/";

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _baseAddress;
    private readonly string _provider; // "openai" or "lm-studio"
    private string _systemPrompt = SystemPrompts.GetSystemPrompt(SystemPrompts.PromptType.Conversation);

    /// <summary>
    /// OpenAI APIを使用して初期化
    /// </summary>
    public OpenAIService(string apiKey, string model = "gpt-4")
    {
        _model = model;
        _provider = "openai";
        _baseAddress = OpenAIBaseAddress;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseAddress),
            Timeout = TimeSpan.FromSeconds(5)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        Log.Info("OpenAIService", $"初期化完了 [OpenAI] - Model: {_model}");
    }

    /// <summary>
    /// LM Studio互換API用コンストラクタ（従来の使用方法）
    /// </summary>
    public OpenAIService(string endpoint, string model, bool isLMStudio)
    {
        _model = model;
        _provider = "lm-studio";
        _baseAddress = endpoint.TrimEnd('/') + "/v1/";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseAddress),
            Timeout = TimeSpan.FromSeconds(5)
        };
        // LM Studioの場合はAuthorizationヘッダーは不要

        Log.Info("OpenAIService", $"初期化完了 [LM Studio] - BaseAddress: {_baseAddress}, Model: {_model}");
    }

    /// <summary>
    /// 汎用初期化コンストラクタ（複数プロバイダー対応）
    /// </summary>
    public OpenAIService(string provider, string endpoint, string model, string? apiKey = null)
    {
        _provider = provider.ToLower();
        _model = model;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        if (_provider == "openai")
        {
            _baseAddress = OpenAIBaseAddress;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("OpenAI APIキーが必要です", nameof(apiKey));
            }
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            Log.Info("OpenAIService", $"初期化完了 [OpenAI] - Model: {_model}");
        }
        else if (_provider == "lm-studio")
        {
            _baseAddress = endpoint.TrimEnd('/') + "/v1/";
            // LM Studioの場合はAuthorizationヘッダーは不要
            Log.Info("OpenAIService", $"初期化完了 [LM Studio] - BaseAddress: {_baseAddress}, Model: {_model}");
        }
        else
        {
            throw new ArgumentException($"サポートされていないプロバイダー: {provider}", nameof(provider));
        }

        _httpClient.BaseAddress = new Uri(_baseAddress);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON models are preserved")]
    public async Task<string> SendMessageAsync(List<Message> conversationHistory)
    {
        try
        {
            Log.Info("OpenAIService", $"リクエスト開始 - URL: {_httpClient.BaseAddress}chat/completions, メッセージ数: {conversationHistory.Count}");

            var request = BuildRequest(conversationHistory);
            Log.Debug("OpenAIService", $"リクエストボディ作成完了");

            var response = await _httpClient.PostAsync("chat/completions", request);
            Log.Info("OpenAIService", $"レスポンス受信 - Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("OpenAIService", $"APIエラー ({response.StatusCode}): {errorContent}");
                throw new HttpRequestException($"API Error ({response.StatusCode}): {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            Log.Debug("OpenAIService", $"レスポンス内容: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");

            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

            if (openAIResponse?.Choices == null || openAIResponse.Choices.Count == 0)
            {
                Log.Error("OpenAIService", "APIからの応答が空です");
                throw new InvalidOperationException("APIからの応答が空です");
            }

            var result = openAIResponse.Choices[0].Message.Content;
            Log.Info("OpenAIService", $"レスポンス解析成功: {result.Substring(0, Math.Min(100, result.Length))}...");
            return result;
        }
        catch (TaskCanceledException ex)
        {
            Log.Error("OpenAIService", $"タイムアウト: {ex.Message}");
            throw new TimeoutException("APIリクエストがタイムアウトしました");
        }
        catch (HttpRequestException ex)
        {
            Log.Error("OpenAIService", $"HTTP通信エラー: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("OpenAIService", $"予期しないエラー: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON models are preserved")]
    private StringContent BuildRequest(List<Message> conversationHistory)
    {
        var apiMessages = new List<ApiMessage>
        {
            // システムプロンプトを最初に追加
            new ApiMessage
            {
                Role = "system",
                Content = _systemPrompt
            }
        };

        // 会話履歴を追加
        apiMessages.AddRange(conversationHistory.Select(m => new ApiMessage
        {
            Role = m.Role == MessageRole.User ? "user" : "assistant",
            Content = m.Content
        }));

        var request = new OpenAIRequest
        {
            Model = _model,
            Messages = apiMessages,
            Temperature = 0.7
        };

        var json = JsonSerializer.Serialize(request);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// システムプロンプトを設定
    /// </summary>
    public void SetSystemPrompt(SystemPrompts.PromptType promptType)
    {
        _systemPrompt = SystemPrompts.GetSystemPrompt(promptType);
        Log.Info("OpenAIService", $"システムプロンプトを変更: {promptType}");
    }

    /// <summary>
    /// システムプロンプトをカスタム文字列で設定
    /// </summary>
    public void SetSystemPrompt(string customPrompt)
    {
        _systemPrompt = customPrompt;
        Log.Info("OpenAIService", "システムプロンプトをカスタム文字列で設定");
    }

    /// <summary>
    /// 現在のシステムプロンプトを取得
    /// </summary>
    public string GetSystemPrompt() => _systemPrompt;

    /// <summary>
    /// モック用: APIキーなしでテスト応答を返す
    /// </summary>
    public static async Task<string> SendMessageMockAsync(string userMessage)
    {
        await Task.Delay(1000); // API呼び出しをシミュレート

        var responses = new[]
        {
            "こんにちは!何かお手伝いできることはありますか?",
            "それは興味深い質問ですね。詳しく教えていただけますか?",
            "なるほど、理解しました。他に何か質問はありますか?",
            "お役に立てて嬉しいです!他にも何でもお聞きください。"
        };

        var random = new Random();
        return responses[random.Next(responses.Length)];
    }
}
