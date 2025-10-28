using System.Text.Json.Serialization;

namespace Robin.Models;

/// <summary>
/// アプリケーション設定ファイルの全体構造を表すモデル
/// </summary>
public class Configuration
{
    [JsonPropertyName("llmSettings")]
    public LLMSettings? LLMSettings { get; set; }

    [JsonPropertyName("voiceSettings")]
    public VoiceSettings? VoiceSettings { get; set; }

    [JsonPropertyName("sttSettings")]
    public STTSettings? STTSettings { get; set; }

    [JsonPropertyName("otherSettings")]
    public OtherSettings? OtherSettings { get; set; }

    [JsonPropertyName("systemPromptSettings")]
    public SystemPromptSettings? SystemPromptSettings { get; set; }
}

/// <summary>
/// LLM（言語モデル）関連の設定（複数プロバイダー対応）
/// </summary>
public class LLMSettings
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; } // "lm-studio" or "openai"

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    public LLMSettings() { }

    public LLMSettings(string provider, string endpoint, string modelName, string? apiKey = null, bool isEnabled = false)
    {
        Provider = provider;
        Endpoint = endpoint;
        ModelName = modelName;
        ApiKey = apiKey;
        IsEnabled = isEnabled;
    }
}

/// <summary>
/// 音声認識関連の設定
/// </summary>
public class VoiceSettings
{
    [JsonPropertyName("engine")]
    public string? Engine { get; set; } // "sherpa" or "android-standard"

    [JsonPropertyName("language")]
    public string? Language { get; set; } // "ja", "en", "zh", "ko"

    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }
}

/// <summary>
/// STT（Speech-to-Text）関連の設定（複数プロバイダー対応）
/// </summary>
public class STTSettings
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; } // "google", "azure", "sherpa-onnx", "android-standard"

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; } // "ja", "en", "zh", "ko", "auto"

    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; } // For Sherpa-ONNX model specification

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    public STTSettings() { }

    public STTSettings(string provider, string? endpoint = null, string? apiKey = null, string? language = null, string? modelName = null, bool isEnabled = false)
    {
        Provider = provider;
        Endpoint = endpoint;
        ApiKey = apiKey;
        Language = language;
        ModelName = modelName;
        IsEnabled = isEnabled;
    }
}

/// <summary>
/// その他の設定（将来的な拡張用）
/// </summary>
public class OtherSettings
{
    [JsonPropertyName("verboseLogging")]
    public bool VerboseLogging { get; set; }

    [JsonPropertyName("theme")]
    public string? Theme { get; set; }
}

/// <summary>
/// システムプロンプト関連の設定
/// Conversation プロンプトと SemanticValidation プロンプトをカスタマイズ可能
/// </summary>
public class SystemPromptSettings
{
    [JsonPropertyName("conversationPrompt")]
    public string? ConversationPrompt { get; set; }

    [JsonPropertyName("semanticValidationPrompt")]
    public string? SemanticValidationPrompt { get; set; }

    [JsonPropertyName("useCustomPrompts")]
    public bool UseCustomPrompts { get; set; } = false;

    public SystemPromptSettings() { }

    public SystemPromptSettings(string? conversationPrompt = null, string? semanticValidationPrompt = null, bool useCustomPrompts = false)
    {
        ConversationPrompt = conversationPrompt;
        SemanticValidationPrompt = semanticValidationPrompt;
        UseCustomPrompts = useCustomPrompts;
    }
}
