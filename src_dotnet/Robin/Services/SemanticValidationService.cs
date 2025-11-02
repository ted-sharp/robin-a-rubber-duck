using System.Text.Json;
using System.Text.Json.Serialization;
using Android.Util;
using Robin.Models;

namespace Robin.Services;

/// <summary>
/// 音声認識結果の意味妥当性をLLMで判定し、誤認識を修正
/// </summary>
public class SemanticValidationService
{
    private readonly OpenAIService? _llmService;
    private readonly SettingsService? _settingsService;

    /// <summary>
    /// 意味妥当性判定完了時のイベント
    /// </summary>
    public event EventHandler<SemanticValidationResult>? ValidationComplete;

    public SemanticValidationService(OpenAIService? llmService = null, SettingsService? settingsService = null)
    {
        _llmService = llmService;
        _settingsService = settingsService;
    }

    /// <summary>
    /// 音声認識テキストの意味妥当性を判定
    /// </summary>
    /// <param name="recognizedText">音声認識結果のテキスト</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>意味妥当性判定結果</returns>
    public async Task<SemanticValidationResult> ValidateAsync(string recognizedText, CancellationToken cancellationToken = default)
    {
        if (_llmService == null)
        {
            Log.Warn("SemanticValidationService", "LLMサービスが初期化されていません");
            return new SemanticValidationResult
            {
                IsSemanticValid = true,
                CorrectedText = recognizedText,
                Feedback = "LLMサービス未初期化のためスキップ"
            };
        }

        try
        {
            // 意味検証用のシステムプロンプトに切り替え
            var originalPrompt = _llmService.GetSystemPrompt();

            // カスタムセマンティック検証プロンプットを優先的に使用
            var semanticPrompt = GetSemanticValidationPrompt();
            _llmService.SetSystemPrompt(semanticPrompt);
            Log.Info("SemanticValidationService",
                $"セマンティック検証プロンプットを適用: {(string.IsNullOrEmpty(semanticPrompt) ? "デフォルト" : "カスタム")}");

            // LLMに意味妥当性と誤認識修正をリクエスト
            var prompt = BuildValidationPrompt(recognizedText);
            var messages = new List<Message>
            {
                new Message
                {
                    Role = MessageRole.User,
                    Content = prompt,
                    DisplayState = MessageDisplayState.Final
                }
            };

            // キャンセルチェック
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _llmService.SendMessageAsync(messages, cancellationToken);

            // 元のシステムプロンプトに戻す
            _llmService.SetSystemPrompt(originalPrompt);

            if (string.IsNullOrEmpty(response))
            {
                return new SemanticValidationResult
                {
                    IsSemanticValid = true,
                    CorrectedText = recognizedText,
                    Feedback = "LLM応答なし"
                };
            }

            // JSON形式の応答をパース
            var result = ParseValidationResponse(response, recognizedText);
            Log.Info("SemanticValidationService", $"意味判定完了: Valid={result.IsSemanticValid}, Corrected={result.CorrectedText}");

            ValidationComplete?.Invoke(this, result);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error("SemanticValidationService", $"意味妥当性判定エラー: {ex.Message}");

            // エラー時も元のシステムプロンプトに戻す
            try
            {
                if (_llmService != null)
                {
                    var originalPrompt = _llmService.GetSystemPrompt();
                    _llmService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);
                }
            }
            catch (Exception restoreEx)
            {
                Log.Warn("SemanticValidationService", $"プロンプト復帰エラー: {restoreEx.Message}");
            }

            // 通常のLLMエラーと同様にエラーを上位に伝播
            throw;
        }
    }

    /// <summary>
    /// セマンティック検証用プロンプットを取得（カスタム優先、なければデフォルト）
    /// </summary>
    private string GetSemanticValidationPrompt()
    {
        try
        {
            // SettingsService が初期化されていて、カスタムプロンプットが設定されている場合
            if (_settingsService != null)
            {
                var promptSettings = _settingsService.LoadSystemPromptSettings();
                if (promptSettings.UseCustomPrompts && !string.IsNullOrEmpty(promptSettings.SemanticValidationPrompt))
                {
                    Log.Debug("SemanticValidationService", "カスタム SemanticValidation プロンプットを使用");
                    return promptSettings.SemanticValidationPrompt;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("SemanticValidationService", $"カスタムプロンプット読み込みエラー: {ex.Message}");
        }

        // デフォルトプロンプットを使用
        Log.Debug("SemanticValidationService", "デフォルト SemanticValidation プロンプットを使用");
        return SystemPrompts.GetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
    }

    /// <summary>
    /// LLMへの判定リクエストプロンプトを構築
    /// </summary>
    private string BuildValidationPrompt(string recognizedText)
    {
        return $@"以下の音声認識結果の意味妥当性を判定し、必要に応じて修正してください。

音声認識結果: ""{recognizedText}""";
    }

    /// <summary>
    /// LLM応答をパース
    /// </summary>
    private SemanticValidationResult ParseValidationResponse(string response, string originalText)
    {
        try
        {
            // レスポンスからJSONを抽出（```json```タグがあるかもしれない）
            var jsonText = ExtractJsonFromResponse(response);

            var parsed = JsonSerializer.Deserialize(jsonText, RobinJsonContext.Default.SemanticValidationResponse);

            if (parsed == null)
            {
                return new SemanticValidationResult
                {
                    IsSemanticValid = true,
                    CorrectedText = originalText,
                    Feedback = "パース失敗"
                };
            }

            return new SemanticValidationResult
            {
                IsSemanticValid = parsed.IsSemanticValid,
                CorrectedText = parsed.CorrectedText ?? originalText,
                Feedback = parsed.Feedback ?? "判定完了"
            };
        }
        catch (JsonException ex)
        {
            Log.Warn("SemanticValidationService", $"JSON解析エラー: {ex.Message}");
            return new SemanticValidationResult
            {
                IsSemanticValid = true,
                CorrectedText = originalText,
                Feedback = "JSON解析失敗"
            };
        }
    }

    /// <summary>
    /// レスポンスからJSON部分を抽出
    /// </summary>
    private string ExtractJsonFromResponse(string response)
    {
        // ```json``` で囲まれている場合は抽出
        var startMarker = "```json";
        var endMarker = "```";

        int startIndex = response.IndexOf(startMarker);
        if (startIndex >= 0)
        {
            startIndex += startMarker.Length;
            int endIndex = response.IndexOf(endMarker, startIndex);
            if (endIndex >= 0)
            {
                return response.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }

        // マーカーがない場合は最初の { から最後の } までを抽出
        startIndex = response.IndexOf('{');
        int endIndexBrace = response.LastIndexOf('}');
        if (startIndex >= 0 && endIndexBrace > startIndex)
        {
            return response.Substring(startIndex, endIndexBrace - startIndex + 1);
        }

        return response;
    }

    /// <summary>
    /// LLM応答の内部パース用クラス
    /// </summary>
    public class SemanticValidationResponse
    {
        [JsonPropertyName("isSemanticValid")]
        public bool IsSemanticValid { get; set; }

        [JsonPropertyName("correctedText")]
        public string? CorrectedText { get; set; }

        [JsonPropertyName("feedback")]
        public string? Feedback { get; set; }
    }
}
