using System.Text.Json;
using Android.Content;
using Android.Util;
using Robin.Models;

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

    // LLMプロバイダーコレクション設定キー（新版：複数設定対応）
    private const string LLMProviderCollectionKey = "llm_provider_collection";

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

    // SharedPreferences サイズ制限（推奨値：1個の設定キーあたり最大1MB）
    private const long MaxPromptSizeBytes = 1024 * 1024; // 1MB

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
    /// LLMプロバイダーコレクションを保存（複数設定対応）
    /// </summary>
    public void SaveLLMProviderCollection(LLMProviderCollection collection)
    {
        try
        {
            var json = JsonSerializer.Serialize(collection);
            var editor = _preferences.Edit();
            if (editor != null)
            {
                editor.PutString(LLMProviderCollectionKey, json);
                editor.Commit();
                Log.Info("SettingsService", "LLMプロバイダーコレクションを保存しました");
            }
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService", $"LLMプロバイダーコレクション保存エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// LLMプロバイダーコレクションを読み込み（複数設定対応）
    /// </summary>
    public LLMProviderCollection LoadLLMProviderCollection()
    {
        try
        {
            var json = _preferences.GetString(LLMProviderCollectionKey, null);
            if (!string.IsNullOrEmpty(json))
            {
                var collection = JsonSerializer.Deserialize<LLMProviderCollection>(json);
                if (collection != null)
                {
                    Log.Info("SettingsService", "LLMプロバイダーコレクションを読み込みました");
                    return collection;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService", $"LLMプロバイダーコレクション読み込みエラー: {ex.Message}");
        }

        // デフォルト値を返す
        Log.Info("SettingsService", "デフォルトのLLMプロバイダーコレクションを使用します");
        return new LLMProviderCollection();
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
        try
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
                // systemPromptSettings が部分的に設定されている場合もチェック
                if (ValidateSystemPromptSettings(config.SystemPromptSettings))
                {
                    SaveSystemPromptSettings(config.SystemPromptSettings);
                    Log.Info("SettingsService", $"システムプロンプント設定を適用しました");
                }
                else
                {
                    Log.Warn("SettingsService", "システムプロンプト設定が検証に失敗、適用をスキップ");
                }
            }
            else
            {
                Log.Debug("SettingsService", "JSON設定ファイルにシステムプロンプト設定が含まれていません");
            }
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService", $"設定適用エラー: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// JSON から読み込んだシステムプロンプト設定を検証
    /// </summary>
    private bool ValidateSystemPromptSettings(SystemPromptSettings? settings)
    {
        if (settings == null)
        {
            Log.Warn("SettingsService", "システムプロンプト設定が null");
            return false;
        }

        try
        {
            // useCustomPrompts が false の場合は検証をスキップ
            if (!settings.UseCustomPrompts)
            {
                Log.Debug("SettingsService", "UseCustomPrompts=false のため、プロンプット検証をスキップ");
                return true;
            }

            // ConversationPrompt の検証
            if (!string.IsNullOrEmpty(settings.ConversationPrompt))
            {
                var convBytes = System.Text.Encoding.UTF8.GetByteCount(settings.ConversationPrompt);
                if (convBytes > MaxPromptSizeBytes)
                {
                    Log.Error("SettingsService",
                        $"JSON Conversation プロンプットサイズ超過: {convBytes} > {MaxPromptSizeBytes}");
                    return false;
                }
                Log.Debug("SettingsService", $"JSON Conversation プロンプット検証成功: {convBytes} バイト");
            }

            // SemanticValidationPrompt の検証
            if (!string.IsNullOrEmpty(settings.SemanticValidationPrompt))
            {
                var semBytes = System.Text.Encoding.UTF8.GetByteCount(settings.SemanticValidationPrompt);
                if (semBytes > MaxPromptSizeBytes)
                {
                    Log.Error("SettingsService",
                        $"JSON SemanticValidation プロンプットサイズ超過: {semBytes} > {MaxPromptSizeBytes}");
                    return false;
                }
                Log.Debug("SettingsService", $"JSON SemanticValidation プロンプット検証成功: {semBytes} バイト");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService", $"システムプロンプト設定検証エラー: {ex.Message}");
            return false;
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
    /// システムプロンプト設定を保存（サイズチェック付き）
    /// </summary>
    public void SaveSystemPromptSettings(SystemPromptSettings settings)
    {
        try
        {
            // 各プロンプットのサイズをチェック
            var conversationPrompt = settings.ConversationPrompt ?? "";
            var semanticValidationPrompt = settings.SemanticValidationPrompt ?? "";

            var convBytes = System.Text.Encoding.UTF8.GetByteCount(conversationPrompt);
            var semBytes = System.Text.Encoding.UTF8.GetByteCount(semanticValidationPrompt);

            // サイズログを出力
            Log.Debug("SettingsService",
                $"プロンプットサイズ - Conversation: {convBytes} バイト ({conversationPrompt.Length} 文字), " +
                $"SemanticValidation: {semBytes} バイト ({semanticValidationPrompt.Length} 文字)");

            // 個別のサイズチェック
            if (convBytes > MaxPromptSizeBytes)
            {
                Log.Error("SettingsService",
                    $"Conversation プロンプットが大きすぎます（{convBytes} > {MaxPromptSizeBytes} バイト）");
                throw new ArgumentException(
                    $"Conversation プロンプットが大きすぎます（最大 1MB）");
            }

            if (semBytes > MaxPromptSizeBytes)
            {
                Log.Error("SettingsService",
                    $"SemanticValidation プロンプットが大きすぎます（{semBytes} > {MaxPromptSizeBytes} バイト）");
                throw new ArgumentException(
                    $"SemanticValidation プロンプットが大きすぎます（最大 1MB）");
            }

            var editor = _preferences.Edit();
            if (editor != null)
            {
                editor.PutString(ConversationPromptKey, conversationPrompt);
                editor.PutString(SemanticValidationPromptKey, semanticValidationPrompt);
                editor.PutBoolean(UseCustomPromptsKey, settings.UseCustomPrompts);
                editor.Commit();

                Log.Info("SettingsService",
                    $"システムプロンプト設定を保存成功 - " +
                    $"カスタムプロンプト使用: {settings.UseCustomPrompts}, " +
                    $"合計サイズ: {convBytes + semBytes} バイト");
            }
        }
        catch (ArgumentException ex)
        {
            Log.Error("SettingsService", $"プロンプット設定保存エラー（検証失敗）: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService", $"プロンプット設定保存エラー（予期しないエラー）: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// システムプロンプト設定を読み込む（ログ付き）
    /// </summary>
    public SystemPromptSettings LoadSystemPromptSettings()
    {
        try
        {
            var conversationPrompt = _preferences.GetString(ConversationPromptKey, "") ?? "";
            var semanticValidationPrompt = _preferences.GetString(SemanticValidationPromptKey, "") ?? "";
            var useCustomPrompts = _preferences.GetBoolean(UseCustomPromptsKey, false);

            var convBytes = System.Text.Encoding.UTF8.GetByteCount(conversationPrompt);
            var semBytes = System.Text.Encoding.UTF8.GetByteCount(semanticValidationPrompt);

            Log.Debug("SettingsService",
                $"システムプロンプト設定を読み込み - " +
                $"カスタム使用: {useCustomPrompts}, " +
                $"Conversation: {convBytes} バイト ({conversationPrompt.Length} 文字), " +
                $"SemanticValidation: {semBytes} バイト ({semanticValidationPrompt.Length} 文字)");

            return new SystemPromptSettings(
                string.IsNullOrEmpty(conversationPrompt) ? null : conversationPrompt,
                string.IsNullOrEmpty(semanticValidationPrompt) ? null : semanticValidationPrompt,
                useCustomPrompts
            );
        }
        catch (Exception ex)
        {
            Log.Error("SettingsService", $"システムプロンプト設定読み込みエラー: {ex.Message}");
            throw;
        }
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
