namespace Robin.Models;

/// <summary>
/// システムプロンプト設定ファイルのモデル
/// </summary>
public class SystemPromptsConfig
{
    /// <summary>
    /// 通常の会話用プロンプト
    /// </summary>
    public required string ConversationPrompt { get; init; }

    /// <summary>
    /// 意味検証と音声認識補正用プロンプト
    /// </summary>
    public required string SemanticValidationPrompt { get; init; }
}
