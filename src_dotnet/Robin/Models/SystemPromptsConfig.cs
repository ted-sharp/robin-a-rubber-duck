namespace Robin.Models;

/// <summary>
/// システムプロンプト設定ファイルのモデル（system_prompts.json）
/// </summary>
public class SystemPromptsConfig
{
    /// <summary>
    /// デフォルトの通常会話用プロンプト
    /// </summary>
    public string? DefaultConversationPrompt { get; set; }

    /// <summary>
    /// デフォルトの意味検証と音声認識補正用プロンプト
    /// </summary>
    public string? DefaultSemanticValidationPrompt { get; set; }
}

/// <summary>
/// ユーザーコンテキストテンプレート
/// </summary>
public class UserContextTemplate
{
    /// <summary>
    /// テンプレートの名前
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// テンプレートのコンテキスト内容
    /// </summary>
    public string? Context { get; set; }
}
