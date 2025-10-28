using Android.Content;
using Android.Util;
using Robin.Models;
using System.Text.Json;

namespace Robin.Services;

public class SettingsService
{
    private readonly ISharedPreferences _preferences;
    private const string PreferencesName = "robin_settings";

    // LLMプロバイダー設定キー
    private const string LLMProviderKey = "llm_provider";
    private const string LLMEndpointKey = "llm_endpoint";
    private const string LLMApiKeyKey = "llm_api_key";
    private const string LLMModelNameKey = "llm_model_name";
    private const string LLMEnabledKey = "llm_enabled";

    // STTプロバイダー設定キー
    private const string STTProviderKey = "stt_provider";
    private const string STTEndpointKey = "stt_endpoint";
    private const string STTApiKeyKey = "stt_api_key";
    private const string STTLanguageKey = "stt_language";
    private const string STTModelNameKey = "stt_model_name";
    private const string STTEnabledKey = "stt_enabled";

    // システムプロンプト設定キー
    private const string ConversationPromptKey = "conversation_prompt";
    private const string SemanticValidationPromptKey = "semantic_validation_prompt";
    private const string UseCustomPromptsKey = "use_custom_prompts";

    // 旧互換性のための設定キー
    private const string LMStudioEndpointKey = "lm_studio_endpoint";
    private const string LMStudioModelNameKey = "lm_studio_model_name";
    private const string LMStudioEnabledKey = "lm_studio_enabled";

    public SettingsService(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Failed to initialize SharedPreferences");
    }

    /// <summary>
    /// LLMプロバイダー設定を保存（新版：複数プロバイダー対応）
    /// </summary>
    public void SaveLLMProviderSettings(LLMProviderSettings settings)
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.PutString(LLMProviderKey, settings.Provider);
            editor.PutString(LLMEndpointKey, settings.Endpoint);
            editor.PutString(LLMApiKeyKey, settings.ApiKey ?? "");
            editor.PutString(LLMModelNameKey, settings.ModelName);
            editor.PutBoolean(LLMEnabledKey, settings.IsEnabled);
            editor.Commit();
            Log.Info("SettingsService", $"LLMプロバイダー設定を保存: {settings.Provider}");
        }
    }

    /// <summary>
    /// LLMプロバイダー設定を読み込む（新版：複数プロバイダー対応）
    /// </summary>
    public LLMProviderSettings LoadLLMProviderSettings()
    {
        var provider = _preferences.GetString(LLMProviderKey, "lm-studio") ?? "lm-studio";
        var endpoint = _preferences.GetString(LLMEndpointKey, "http://192.168.0.7:1234") ?? "http://192.168.0.7:1234";
        var apiKey = _preferences.GetString(LLMApiKeyKey, "") ?? "";
        var modelName = _preferences.GetString(LLMModelNameKey, provider == "openai" ? "gpt-3.5-turbo" : "openai/gpt-oss-20b") ?? "gpt-3.5-turbo";
        var isEnabled = _preferences.GetBoolean(LLMEnabledKey, true);

        return new LLMProviderSettings(provider, endpoint, modelName, string.IsNullOrWhiteSpace(apiKey) ? null : apiKey, isEnabled);
    }

    /// <summary>
    /// 旧互換性のためのLMStudioSettings保存（廃止予定）
    /// </summary>
    [Obsolete("Use SaveLLMProviderSettings instead")]
    public void SaveLMStudioSettings(LMStudioSettings settings)
    {
        var providerSettings = settings.ToLLMProviderSettings();
        SaveLLMProviderSettings(providerSettings);
    }

    /// <summary>
    /// 旧互換性のためのLMStudioSettings読み込み（廃止予定）
    /// </summary>
    [Obsolete("Use LoadLLMProviderSettings instead")]
    public LMStudioSettings LoadLMStudioSettings()
    {
        var providerSettings = LoadLLMProviderSettings();
        return new LMStudioSettings(providerSettings.Endpoint, providerSettings.ModelName, providerSettings.IsEnabled);
    }

    /// <summary>
    /// 旧設定をクリア（廃止予定）
    /// </summary>
    [Obsolete("Use ClearLLMProviderSettings instead")]
    public void ClearLMStudioSettings()
    {
        ClearLLMProviderSettings();
    }

    /// <summary>
    /// LLMプロバイダー設定をクリア
    /// </summary>
    public void ClearLLMProviderSettings()
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.Remove(LLMProviderKey);
            editor.Remove(LLMEndpointKey);
            editor.Remove(LLMApiKeyKey);
            editor.Remove(LLMModelNameKey);
            editor.Remove(LLMEnabledKey);
            editor.Commit();
            Log.Info("SettingsService", "LLMプロバイダー設定をクリア");
        }
    }

    /// <summary>
    /// JSON設定ファイルから設定を読み込む
    /// </summary>
    public async Task<Configuration?> LoadConfigurationFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Log.Error("SettingsService", $"設定ファイルが見つかりません: {filePath}");
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<Configuration>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false
            });

            Log.Info("SettingsService", $"設定ファイルを読み込みました: {filePath}");
            return config;
        }
        catch (JsonException ex)
        {
            Log.Error("SettingsService", $"JSON解析エラー: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService", $"設定ファイル読み込みエラー: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 読み込んだ設定を SharedPreferences に保存する
    /// </summary>
    public void ApplyConfiguration(Configuration config)
    {
        if (config?.LLMSettings != null)
        {
            var provider = config.LLMSettings.Provider ?? "lm-studio";
            var settings = new LLMProviderSettings(
                provider,
                config.LLMSettings.Endpoint ?? "http://192.168.0.7:1234",
                config.LLMSettings.ModelName ?? "gpt-3.5-turbo",
                config.LLMSettings.ApiKey,
                config.LLMSettings.IsEnabled
            );
            SaveLLMProviderSettings(settings);
            Log.Info("SettingsService", $"LLM設定を適用しました [{provider}]");
        }

        if (config?.SystemPromptSettings != null)
        {
            SaveSystemPromptSettings(config.SystemPromptSettings);
            Log.Info("SettingsService", $"システムプロンプト設定を適用しました");
        }
    }

    /// <summary>
    /// STTプロバイダー設定を保存
    /// </summary>
    public void SaveSTTProviderSettings(STTProviderSettings settings)
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.PutString(STTProviderKey, settings.Provider);
            editor.PutString(STTEndpointKey, settings.Endpoint ?? "");
            editor.PutString(STTApiKeyKey, settings.ApiKey ?? "");
            editor.PutString(STTLanguageKey, settings.Language ?? "");
            editor.PutString(STTModelNameKey, settings.ModelName ?? "");
            editor.PutBoolean(STTEnabledKey, settings.IsEnabled);
            editor.Commit();
            Log.Info("SettingsService", $"STT設定を保存: {settings.Provider}");
        }
    }

    /// <summary>
    /// STTプロバイダー設定を読み込む
    /// </summary>
    public STTProviderSettings LoadSTTProviderSettings()
    {
        var provider = _preferences.GetString(STTProviderKey, "sherpa-onnx") ?? "sherpa-onnx";
        var endpoint = _preferences.GetString(STTEndpointKey, "") ?? "";
        var apiKey = _preferences.GetString(STTApiKeyKey, "") ?? "";
        var language = _preferences.GetString(STTLanguageKey, "auto") ?? "auto";
        var modelName = _preferences.GetString(STTModelNameKey, "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09") ?? "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09";
        var isEnabled = _preferences.GetBoolean(STTEnabledKey, true);

        return new STTProviderSettings(
            provider,
            string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            language,
            modelName,
            isEnabled
        );
    }

    /// <summary>
    /// STT設定をクリア
    /// </summary>
    public void ClearSTTProviderSettings()
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.Remove(STTProviderKey);
            editor.Remove(STTEndpointKey);
            editor.Remove(STTApiKeyKey);
            editor.Remove(STTLanguageKey);
            editor.Remove(STTModelNameKey);
            editor.Remove(STTEnabledKey);
            editor.Commit();
            Log.Info("SettingsService", "STT設定をクリア");
        }
    }

    /// <summary>
    /// システムプロンプト設定を保存
    /// </summary>
    public void SaveSystemPromptSettings(SystemPromptSettings settings)
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.PutString(ConversationPromptKey, settings.ConversationPrompt ?? "");
            editor.PutString(SemanticValidationPromptKey, settings.SemanticValidationPrompt ?? "");
            editor.PutBoolean(UseCustomPromptsKey, settings.UseCustomPrompts);
            editor.Commit();
            Log.Info("SettingsService", $"システムプロンプト設定を保存（カスタムプロンプト使用: {settings.UseCustomPrompts}）");
        }
    }

    /// <summary>
    /// システムプロンプト設定を読み込む
    /// </summary>
    public SystemPromptSettings LoadSystemPromptSettings()
    {
        var conversationPrompt = _preferences.GetString(ConversationPromptKey, "") ?? "";
        var semanticValidationPrompt = _preferences.GetString(SemanticValidationPromptKey, "") ?? "";
        var useCustomPrompts = _preferences.GetBoolean(UseCustomPromptsKey, false);

        return new SystemPromptSettings(
            string.IsNullOrEmpty(conversationPrompt) ? null : conversationPrompt,
            string.IsNullOrEmpty(semanticValidationPrompt) ? null : semanticValidationPrompt,
            useCustomPrompts
        );
    }

    /// <summary>
    /// システムプロンプト設定をクリア
    /// </summary>
    public void ClearSystemPromptSettings()
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.Remove(ConversationPromptKey);
            editor.Remove(SemanticValidationPromptKey);
            editor.Remove(UseCustomPromptsKey);
            editor.Commit();
            Log.Info("SettingsService", "システムプロンプト設定をクリア");
        }
    }

    /// <summary>
    /// 現在の設定を Configuration オブジェクトにエクスポート
    /// </summary>
    public Configuration ExportConfiguration()
    {
        var llmSettings = LoadLLMProviderSettings();
        var sttSettings = LoadSTTProviderSettings();
        var systemPromptSettings = LoadSystemPromptSettings();

        return new Configuration
        {
            LLMSettings = new LLMSettings(
                llmSettings.Provider,
                llmSettings.Endpoint,
                llmSettings.ModelName,
                llmSettings.ApiKey,
                llmSettings.IsEnabled
            ),
            VoiceSettings = new VoiceSettings
            {
                Engine = sttSettings.Provider,
                Language = sttSettings.Language
            },
            STTSettings = new STTSettings(
                sttSettings.Provider,
                sttSettings.Endpoint,
                sttSettings.ApiKey,
                sttSettings.Language,
                sttSettings.ModelName,
                sttSettings.IsEnabled
            ),
            OtherSettings = new OtherSettings
            {
                VerboseLogging = false,
                Theme = "light"
            },
            SystemPromptSettings = systemPromptSettings
        };
    }
}
