using System.Text.Json.Serialization;

namespace Robin.Models;

/// <summary>
/// STTプロバイダー設定（Google Speech-to-Text、Azure、Sherpa-ONNX等の複数プロバイダーに対応）
/// </summary>
public class STTProviderSettings
{
    /// <summary>
    /// プロバイダータイプ: "google", "azure", "sherpa-onnx", "android-standard"
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "sherpa-onnx";

    /// <summary>
    /// APIエンドポイント（クラウドサービス使用時）
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    /// <summary>
    /// APIキー（クラウドサービス使用時に必須）
    /// </summary>
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// 言語設定: "ja", "en", "zh", "ko", "auto"
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// モデル名（Sherpa-ONNXの場合に使用）
    /// </summary>
    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }

    /// <summary>
    /// STT機能が有効かどうか
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    public STTProviderSettings()
    {
    }

    public STTProviderSettings(
        string provider,
        string? endpoint = null,
        string? apiKey = null,
        string? language = null,
        string? modelName = null,
        bool isEnabled = true)
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
/// Google Cloud Speech-to-Text 設定
/// </summary>
public class GoogleSTTSettings : STTProviderSettings
{
    public GoogleSTTSettings(string apiKey, string language = "ja-JP", bool isEnabled = true)
        : base("google", "https://speech.googleapis.com/v1/speech:recognize", apiKey, language, null, isEnabled)
    {
    }
}

/// <summary>
/// Azure Cognitive Services Speech-to-Text 設定
/// </summary>
public class AzureSTTSettings : STTProviderSettings
{
    public AzureSTTSettings(string region, string apiKey, string language = "ja-JP", bool isEnabled = true)
        : base("azure", $"https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1", apiKey, language, "azure-stt", isEnabled)
    {
    }
}

/// <summary>
/// Sherpa-ONNX（オフライン音声認識）設定
/// </summary>
public class SherpaOnnxSTTSettings : STTProviderSettings
{
    public SherpaOnnxSTTSettings(string modelName = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09", string language = "auto", bool isEnabled = true)
        : base("sherpa-onnx", null, null, language, modelName, isEnabled)
    {
    }
}

/// <summary>
/// Android標準音声認識設定
/// </summary>
public class AndroidSTTSettings : STTProviderSettings
{
    public AndroidSTTSettings(string language = "ja-JP", bool isEnabled = true)
        : base("android-standard", null, null, language, "android-default", isEnabled)
    {
    }
}

/// <summary>
/// Faster Whisper（LAN内サーバー）設定
/// </summary>
public class FasterWhisperSTTSettings : STTProviderSettings
{
    public FasterWhisperSTTSettings(string serverUrl, string language = "ja", bool isEnabled = true)
        : base("faster-whisper", serverUrl, null, language, "faster-whisper", isEnabled)
    {
    }
}
