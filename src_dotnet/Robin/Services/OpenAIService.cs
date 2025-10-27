using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Android.Util;
using Robin.Models;

namespace Robin.Services;

public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseAddress;

    public OpenAIService(string apiKey, string model = "gpt-4")
    {
        _apiKey = apiKey;
        _model = model;
        _baseAddress = "https://api.openai.com/v1/";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseAddress),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    // LM Studio互換API用コンストラクタ
    public OpenAIService(string endpoint, string model, bool isLMStudio)
    {
        _apiKey = "lm-studio"; // LM StudioはAPIキー不要
        _model = model;
        _baseAddress = endpoint.TrimEnd('/') + "/v1/";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseAddress),
            Timeout = TimeSpan.FromSeconds(60)
        };
        // LM Studioの場合はAuthorizationヘッダーは不要
        if (!isLMStudio)
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        // デバッグログ
        Log.Info("OpenAIService", $"初期化完了 - BaseAddress: {_baseAddress}, Model: {_model}, IsLMStudio: {isLMStudio}");
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
    private HttpContent BuildRequest(List<Message> conversationHistory)
    {
        var apiMessages = conversationHistory.Select(m => new ApiMessage
        {
            Role = m.Role == MessageRole.User ? "user" : "assistant",
            Content = m.Content
        }).ToList();

        var request = new OpenAIRequest
        {
            Model = _model,
            Messages = apiMessages,
            Temperature = 0.7
        };

        var json = JsonSerializer.Serialize(request);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    // モック用: APIキーなしでテスト応答を返す
    public async Task<string> SendMessageMockAsync(string userMessage)
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
