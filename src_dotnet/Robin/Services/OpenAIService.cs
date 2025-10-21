using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Robin.Models;

namespace Robin.Services;

public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAIService(string apiKey, string model = "gpt-4")
    {
        _apiKey = apiKey;
        _model = model;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "JSON models are preserved")]
    public async Task<string> SendMessageAsync(List<Message> conversationHistory)
    {
        try
        {
            var request = BuildRequest(conversationHistory);
            var response = await _httpClient.PostAsync("chat/completions", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API Error ({response.StatusCode}): {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

            if (openAIResponse?.Choices == null || openAIResponse.Choices.Count == 0)
            {
                throw new InvalidOperationException("APIからの応答が空です");
            }

            return openAIResponse.Choices[0].Message.Content;
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("APIリクエストがタイムアウトしました");
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
