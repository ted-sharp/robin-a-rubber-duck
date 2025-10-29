using System.Text.Json.Serialization;

namespace Robin.Models;

/// <summary>
/// LLMプロバイダー設定（LM Studio、OpenAIなど複数のプロバイダーに対応）
/// </summary>
public class LLMProviderSettings
{
    /// <summary>
    /// プロバイダータイプ: "lm-studio" または "openai"
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "lm-studio";

    /// <summary>
    /// APIエンドポイント（LM Studio使用時、またはOpenAI互換のカスタムエンドポイント）
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "http://192.168.0.7:1234";

    /// <summary>
    /// APIキー（OpenAI使用時に必須）
    /// </summary>
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// 使用するモデル名
    /// </summary>
    [JsonPropertyName("modelName")]
    public string ModelName { get; set; } = "gpt-3.5-turbo";

    /// <summary>
    /// LLM機能が有効かどうか
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    public LLMProviderSettings()
    {
    }

    public LLMProviderSettings(string provider, string endpoint, string modelName, string? apiKey = null, bool isEnabled = false)
    {
        Provider = provider;
        Endpoint = endpoint;
        ModelName = modelName;
        ApiKey = apiKey;
        IsEnabled = isEnabled;
    }
}

/// <summary>
/// 旧互換性のためのLMStudioSettings（廃止予定）
/// </summary>
[Obsolete("Use LLMProviderSettings instead")]
public class LMStudioSettings
{
    public string Endpoint { get; set; } = "http://192.168.0.7:1234";
    public string ModelName { get; set; } = "openai/gpt-oss-20b";
    public bool IsEnabled { get; set; } = true;

    public LMStudioSettings()
    {
    }

    public LMStudioSettings(string endpoint, string modelName, bool isEnabled = false)
    {
        Endpoint = endpoint;
        ModelName = modelName;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// LLMProviderSettings に変換
    /// </summary>
    public LLMProviderSettings ToLLMProviderSettings()
    {
        return new LLMProviderSettings("lm-studio", Endpoint, ModelName, null, IsEnabled);
    }
}
