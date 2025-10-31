namespace Robin.Models;

/// <summary>
/// システムプロンプト設定ファイルのモデル
/// </summary>
public class SystemPromptsConfig
{
    /// <summary>
    /// 通常の会話用プロンプト
    /// </summary>
    public string? ConversationPrompt { get; set; }

    /// <summary>
    /// 意味検証と音声認識補正用プロンプト
    /// </summary>
    public string? SemanticValidationPrompt { get; set; }
}
