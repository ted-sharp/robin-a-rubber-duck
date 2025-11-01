using Android.Content;
using System.Text.Json;

namespace Robin.Models;

/// <summary>
/// LLMのシステムプロンプト定義
/// 通常の会話（ラバーダックデバッグ）と意味検証の2種類を提供
/// JSONファイルから読み込み、フォールバック用のデフォルトプロンプトを保持
/// </summary>
public static class SystemPrompts
{
    private static string? _conversationPrompt;
    private static string? _semanticValidationPrompt;

    /// <summary>
    /// デフォルトの通常会話用システムプロンプト（フォールバック用）
    /// テキストファイルが読み込めない場合のみ使用される
    /// </summary>
    private const string DefaultConversationSystemPrompt =
        "あなたはRobinという名前のラバーダックです。簡潔に、要点を絞って回答してください。";

    /// <summary>
    /// デフォルトの意味検証と音声認識補正用システムプロンプト（フォールバック用）
    /// テキストファイルが読み込めない場合のみ使用される
    /// </summary>
    private const string DefaultSemanticValidationSystemPrompt =
        "音声認識結果を分析し、JSON形式で {\"isSemanticValid\": boolean, \"correctedText\": string, \"feedback\": string} を返してください。";

    /// <summary>
    /// テキストファイルからシステムプロンプトを読み込む
    /// Assets/Resources/raw/conversation_prompt.txt と semantic_validation_prompt.txt から読み込みます
    /// </summary>
    public static void LoadFromFiles(Context context)
    {
        try
        {
            // conversation_prompt.txt を読み込む
            try
            {
                using var stream = context.Assets?.Open("Resources/raw/conversation_prompt.txt");
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    _conversationPrompt = reader.ReadToEnd();
                    Android.Util.Log.Info("SystemPrompts", $"Loaded conversation_prompt.txt ({_conversationPrompt.Length} chars)");
                }
                else
                {
                    Android.Util.Log.Warn("SystemPrompts", "conversation_prompt.txt not found, using default");
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("SystemPrompts", $"Error loading conversation_prompt.txt: {ex.Message}");
            }

            // semantic_validation_prompt.txt を読み込む
            try
            {
                using var stream = context.Assets?.Open("Resources/raw/semantic_validation_prompt.txt");
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    _semanticValidationPrompt = reader.ReadToEnd();
                    Android.Util.Log.Info("SystemPrompts", $"Loaded semantic_validation_prompt.txt ({_semanticValidationPrompt.Length} chars)");
                }
                else
                {
                    Android.Util.Log.Warn("SystemPrompts", "semantic_validation_prompt.txt not found, using default");
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("SystemPrompts", $"Error loading semantic_validation_prompt.txt: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("SystemPrompts", $"Error loading system prompts: {ex.Message}\nStack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 通常の会話用システムプロンプトを取得
    /// </summary>
    public static string ConversationSystemPrompt =>
        _conversationPrompt ?? DefaultConversationSystemPrompt;

    /// <summary>
    /// 意味検証と音声認識補正用システムプロンプトを取得
    /// </summary>
    public static string SemanticValidationSystemPrompt =>
        _semanticValidationPrompt ?? DefaultSemanticValidationSystemPrompt;

    /// <summary>
    /// システムプロンプトのタイプを識別
    /// </summary>
    public enum PromptType
    {
        /// <summary>通常の会話（ラバーダックデバッグ）</summary>
        Conversation,

        /// <summary>意味検証と音声認識補正</summary>
        SemanticValidation
    }

    /// <summary>
    /// 指定されたプロンプトタイプに対応するシステムプロンプトを取得
    /// </summary>
    public static string GetSystemPrompt(PromptType promptType)
    {
        return promptType switch
        {
            PromptType.Conversation => ConversationSystemPrompt,
            PromptType.SemanticValidation => SemanticValidationSystemPrompt,
            _ => ConversationSystemPrompt
        };
    }
}
