namespace Robin.Models;

public enum MessageRole
{
    User,
    Assistant
}

/// <summary>
/// 音声認識の意味妥当性判定結果
/// </summary>
public class SemanticValidationResult
{
    /// <summary>
    /// 意味が通じるかどうか
    /// </summary>
    public bool IsSemanticValid { get; set; }

    /// <summary>
    /// 修正後の音声認識テキスト（誤認識修正済み）
    /// </summary>
    public string CorrectedText { get; set; } = string.Empty;

    /// <summary>
    /// LLMからのフィードバック/理由
    /// </summary>
    public string Feedback { get; set; } = string.Empty;

    /// <summary>
    /// 検証時刻
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.Now;
}

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 音声認識結果の場合、元の認識テキスト（修正前）
    /// </summary>
    public string? OriginalRecognizedText { get; set; }

    /// <summary>
    /// 音声認識結果の意味妥当性判定結果
    /// </summary>
    public SemanticValidationResult? SemanticValidation { get; set; }

    /// <summary>
    /// メッセージの表示状態
    /// RawRecognized = 生の音声認識結果を表示中
    /// SemanticValidated = 意味解析済み結果を表示中
    /// </summary>
    public MessageDisplayState DisplayState { get; set; } = MessageDisplayState.Final;

    public bool IsUser => Role == MessageRole.User;
    public bool IsAssistant => Role == MessageRole.Assistant;

    public string FormattedTime => Timestamp.ToString("HH:mm");

    /// <summary>
    /// 実際に表示するコンテンツを返す
    /// </summary>
    public string GetDisplayContent() =>
        SemanticValidation?.IsSemanticValid == true ?
            (SemanticValidation.CorrectedText ?? Content) :
            Content;

    /// <summary>
    /// 表示状態を取得（認識中 vs 解析済み）
    /// </summary>
    public bool IsSemanticValidationApplied =>
        SemanticValidation?.IsSemanticValid == true;

    /// <summary>
    /// 修正が行われたかどうかを判定
    /// </summary>
    public bool WasCorrected =>
        IsSemanticValidationApplied &&
        !string.IsNullOrEmpty(OriginalRecognizedText) &&
        OriginalRecognizedText != Content;

    /// <summary>
    /// 修正内容の詳細情報を取得
    /// 形式: "修正前: xxx → 修正後: yyy"
    /// </summary>
    public string GetCorrectionDetails() =>
        WasCorrected ?
            $"修正: \"{OriginalRecognizedText}\" → \"{Content}\"" :
            string.Empty;

    /// <summary>
    /// メッセージの詳細情報を取得（修正情報を含む）
    /// </summary>
    public string GetDetailedContent() =>
        $"{GetDisplayContent()}" +
        (WasCorrected ? $"\n[{GetCorrectionDetails()}]" : string.Empty);
}

public enum MessageDisplayState
{
    /// <summary>
    /// 最終的なメッセージ（通常のチャットメッセージ）
    /// </summary>
    Final,

    /// <summary>
    /// 生の音声認識結果を表示中
    /// </summary>
    RawRecognized,

    /// <summary>
    /// 意味妥当性を判定中
    /// </summary>
    ValidatingSemantics,

    /// <summary>
    /// 意味解析済み（修正完了）
    /// </summary>
    SemanticValidated
}
