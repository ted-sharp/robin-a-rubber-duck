# Robin 処理状態表示機能

## 概要

Robin がどのような処理を実行中であるかを、ユーザーに対して絵文字付きのメッセージで表示します。ユーザーは Robin の動作を可視化できるため、応答を待っている間に不安を感じることなく、処理の進行状況を追跡できます。

## 処理状態の種類

### 1. 📥 読み取り中
**状態**: `ReadingAudio`

音声認識結果をバッファに追加している最中を表示します。

```
表示例: "📥 読み取り中... こんにちは"
        (認識されたテキストも表示)
```

**実装位置**: `OnRecognitionResult()`, `OnSherpaFinalResult()`

### 2. ⏱️ 処理待機中
**状態**: `WaitingForBuffer`

バッファがウォッチドッグのタイムアウト（2秒）を待機している状態。複数の音声認識セグメントが統合されるのを待っています。

```
表示例: "⏱️ 処理待機中..."
```

**実装位置**: `OnRecognitionResult()`, `OnSherpaFinalResult()` の後

### 3. 🤔 考え中
**状態**: `EvaluatingMeaning`

LLM に意味の妥当性を判定させている状態。音声認識の誤認識を修正するかどうかを判定中です。

```
表示例: "🤔 考え中..."
```

**実装位置**: `OnInputBufferReady()` 開始時

**所要時間**: 通常 1～3秒（LLM API レスポンス時間）

### 4. ✏️ 確認中
**状態**: `ProcessingText`

意味検証後のテキストが意味妥当性を持つため、修正を適用して確認中。UI の色を変更したり、修正情報を表示したりしている状態です。

```
表示例: "✏️ 確認中..."
```

**実装位置**: `OnInputBufferReady()` の意味検証成功後

**所要時間**: 非常に短い（UI 更新のみ）

### 5. 💭 入力中
**状態**: `WaitingForResponse`

LLM にテキストを送信して、レスポンスを待っている状態。ユーザーの質問や指示に対する AI の回答を生成中です。

```
表示例: "💭 入力中..."
```

**実装位置**: `ProcessValidInput()` 開始時

**所要時間**: 通常 2～10秒（LLM のレスポンス生成時間）

### 6. ❌ エラー発生
**状態**: `Error`

エラーが発生した状態。詳細なエラーメッセージが表示されます。

```
表示例: "❌ エラー発生 タイムアウト"
```

**実装位置**: エラー発生時

### 7. (非表示)
**状態**: `Idle`

処理が完了した状態。ステータステキストは非表示になります。

## 処理フロー

```
ユーザーが音声入力
    ↓
📥 読み取り中... (認識テキスト)
    ↓
⏱️ 処理待機中... (2秒間)
    ↓
🤔 考え中... (LLM で意味判定)
    ↓
    ├─ 意味 OK → ✏️ 確認中... (修正適用)
    │            ↓
    │            💭 入力中... (LLM レスポンス待機)
    │            ↓
    │            (AI メッセージを表示)
    │            ↓
    │            (ステータス非表示)
    │
    └─ 意味 NG → ❌ 意味が通じません...
                (3秒後にステータス非表示)
                次の入力を待機
```

## 実装詳細

### ProcessingState 列挙型

```csharp
public enum RobinProcessingState
{
    Idle,                      // 何もしていない
    ReadingAudio,             // 読み取り中
    WaitingForBuffer,         // 処理待機中
    EvaluatingMeaning,        // 考え中
    ProcessingText,           // 確認中
    WaitingForResponse,       // 入力中
    Error                     // エラー
}
```

### メッセージ管理

```csharp
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

    public static string GetMessage(RobinProcessingState state);
    public static string GetDetailedMessage(RobinProcessingState state, string? details = null);
}
```

### 状態更新メソッド

```csharp
private void UpdateProcessingState(RobinProcessingState newState, string? details = null)
{
    _currentProcessingState = newState;

    RunOnUiThread(() =>
    {
        if (newState == RobinProcessingState.Idle)
        {
            _statusText!.Visibility = ViewStates.Gone;
        }
        else
        {
            _statusText!.Text = ProcessingStateMessages.GetDetailedMessage(newState, details);
            _statusText!.Visibility = ViewStates.Visible;
        }
    });

    // ログ出力
    Android.Util.Log.Debug("MainActivity",
        $"処理状態: {newState} - {ProcessingStateMessages.GetMessage(newState)}");
}
```

## 使用例

### 例 1: 正常な処理フロー

```
ユーザー: 「タスクを作成して」

【時刻 0秒】
📥 読み取り中... タスクを作成して

【時刻 0.1秒】
⏱️ 処理待機中...

【時刻 2秒】
🤔 考え中...
(LLM が意味を判定中...)

【時刻 2.5秒】
✏️ 確認中...
(修正を適用中...)

【時刻 2.6秒】
💭 入力中...
(LLM がレスポンス生成中...)

【時刻 4秒】
(ステータス非表示)
AI: 「タスクを作成しました。以下の項目が...」
```

### 例 2: 誤認識が修正される場合

```
ユーザー: 「タス苦を作成して」（音声：「タスクを...」）

【時刻 0秒】
📥 読み取り中... タス苦を作成して

【時刻 0.1秒】
⏱️ 処理待機中...

【時刻 2秒】
🤔 考え中...
(LLM: "苦" → "ク" に修正)

【時刻 2.5秒】
✏️ 確認中...
(テキストを「タスクを作成して」に修正)

【時刻 2.6秒】
💭 入力中...
(LLM がレスポンス生成中...)

【時刻 4秒】
(ステータス非表示)
ユーザー: タスクを作成して
          [修正: "タス苦を作成して" → "タスクを作成して"]
AI: 「タスクを作成しました。...」
```

### 例 3: 意味が通じない場合

```
ユーザー: 「あぎじばすがぷますぎす」（ノイズ）

【時刻 0秒】
📥 読み取り中... あぎじばすがぷますぎす

【時刻 0.1秒】
⏱️ 処理待機中...

【時刻 2秒】
🤔 考え中...
(LLM: 意味不明)

【時刻 2.5秒】
❌ 意味が通じません。もう一度お願いします。

【時刻 5秒】
(ステータス非表示)
(次の入力を待機)
```

## UI の配置

ステータステキストは `_statusText` TextView に表示されます。

**属性**:
- **ID**: `status_text`
- **位置**: 画面上部（音声認識中の「聞き取り中...」の下）
- **可視性**: 処理中は表示、完了時は非表示
- **文字色**: システムカラー（通常は黒）
- **背景**: 透明

## ログ出力

すべての処理状態変化は Logcat に出力されます。

**ログタグ**: `MainActivity`

### ログ出力例

```
[MainActivity] 処理状態: ReadingAudio - 📥 読み取り中...
[MainActivity] 処理状態: WaitingForBuffer - ⏱️ 処理待機中...
[MainActivity] 処理状態: EvaluatingMeaning - 🤔 考え中...
[MainActivity] 処理状態: ProcessingText - ✏️ 確認中...
[MainActivity] 処理状態: WaitingForResponse - 💭 入力中...
[MainActivity] 処理状態: Idle -
```

## デバッグ方法

### Android Studio Logcat での確認

```bash
# MainActivity のログのみ表示
adb logcat | grep "MainActivity"

# 処理状態の変化を追跡
adb logcat | grep "処理状態"
```

### プログラムでの状態確認

```csharp
// 現在の処理状態を取得
var currentState = _currentProcessingState;

// ログに出力
Android.Util.Log.Info("DebugTag", $"現在の処理状態: {currentState}");
```

## カスタマイズ

### メッセージの変更

`ProcessingStateMessages` クラスの `StateMessages` 辞書を修正：

```csharp
StateMessages = new()
{
    { RobinProcessingState.ReadingAudio, "🎤 聞き取り中..." },  // 変更
    { RobinProcessingState.WaitingForBuffer, "⏳ 準備中..." },   // 変更
    // ...
};
```

### タイムアウト時間の調整

`RecognizedInputBuffer` のコンストラクタで調整：

```csharp
// 現在: 2秒
_inputBuffer = new RecognizedInputBuffer(timeoutMs: 2000);

// 3秒に変更
_inputBuffer = new RecognizedInputBuffer(timeoutMs: 3000);
```

### 絵文字の削除

メッセージから絵文字を削除：

```csharp
StateMessages = new()
{
    { RobinProcessingState.ReadingAudio, "読み取り中..." },
    { RobinProcessingState.WaitingForBuffer, "処理待機中..." },
    // ...
};
```

## パフォーマンス

### メモリ使用量
- 処理状態の管理: 最小限（単一の Enum）
- ログ出力: 無視できるレベル

### レスポンス時間
- 状態更新: < 1ms（UI スレッド上）

## まとめ

### キーポイント

1. **可視化**: Robin がどのような処理を実行中であるかが明確
2. **フィードバック**: ユーザーが応答を待つ間の不安が軽減
3. **デバッグ**: ログで処理フローを追跡可能
4. **拡張性**: 新しい処理状態を容易に追加可能

### 処理状態の順序（通常フロー）

```
Idle → ReadingAudio → WaitingForBuffer → EvaluatingMeaning
→ ProcessingText → WaitingForResponse → Idle
```

### トラブルシューティング

**ステータステキストが表示されない？**
- `UpdateProcessingState()` が呼ばれているか確認
- ログで状態変化を確認
- `_statusText` が null でないか確認

**状態が更新されない？**
- `UpdateProcessingState()` が呼ばれているか確認
- ログレベルが DEBUG 以上に設定されているか確認

## 参考資料

- ProcessingState.cs: `src_dotnet/Robin/Models/ProcessingState.cs`
- MainActivity.cs: `src_dotnet/Robin/MainActivity.cs`
