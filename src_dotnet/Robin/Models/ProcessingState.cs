namespace Robin.Models;

/// <summary>
/// Robin の処理状態を定義
/// </summary>
public enum RobinProcessingState
{
    /// <summary>
    /// アイドル状態（何もしていない）
    /// </summary>
    Idle,

    /// <summary>
    /// 音声認識結果をバッファに読み込み中
    /// </summary>
    ReadingAudio,

    /// <summary>
    /// バッファがウォッチドッグを待機中
    /// </summary>
    WaitingForBuffer,

    /// <summary>
    /// 意味の妥当性を判定中
    /// </summary>
    EvaluatingMeaning,

    /// <summary>
    /// テキストを確認中（修正を適用中）
    /// </summary>
    ProcessingText,

    /// <summary>
    /// LLM のレスポンスを待機中
    /// </summary>
    WaitingForResponse,

    /// <summary>
    /// エラーが発生した
    /// </summary>
    Error
}

/// <summary>
/// 処理状態に対応するメッセージを管理
/// </summary>
public static class ProcessingStateMessages
{
    private static readonly Dictionary<RobinProcessingState, string> StateMessages = new()
    {
        { RobinProcessingState.Idle, "" },
        { RobinProcessingState.ReadingAudio, "📥 読み取り中..." },
        { RobinProcessingState.WaitingForBuffer, "⏱️ 処理待機中..." },
        { RobinProcessingState.EvaluatingMeaning, "🤔 考え中..." },
        { RobinProcessingState.ProcessingText, "✏️ 確認中..." },
        { RobinProcessingState.WaitingForResponse, "💭 入力中..." },
        { RobinProcessingState.Error, "❌ エラー発生" }
    };

    /// <summary>
    /// 処理状態に対応するメッセージを取得
    /// </summary>
    public static string GetMessage(RobinProcessingState state) =>
        StateMessages.TryGetValue(state, out var message) ? message : string.Empty;

    /// <summary>
    /// 処理状態に対応するメッセージを詳細情報付きで取得
    /// </summary>
    public static string GetDetailedMessage(RobinProcessingState state, string? details = null) =>
        string.IsNullOrEmpty(details) ?
            GetMessage(state) :
            $"{GetMessage(state)} {details}";
}
