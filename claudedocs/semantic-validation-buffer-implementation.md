# 音声認識結果の意味検証とバッファリング機構の実装

## 概要

このドキュメントは、Robin アプリケーションに追加された音声認識結果のバッファリング、意味妥当性判定、および2段階UI表示機能について説明しています。

## 実装概要

### 目的

音声認識で得られたテキストが正しく意味を持つかを LLM で判定し、誤認識を修正した上で LLM による処理を実行する機構を実装。また、ユーザーに対して認識結果が徐々に改善される様子を視覚的に示す。

### フロー図

```
音声認識結果
    ↓
RecognizedInputBuffer に追加
    ↓
ウォッチドッグ発火（2秒）
    ↓
バッファコンテンツを取得 → 画面に「生の認識結果」を表示
    ↓
SemanticValidationService で意味判定
    ↓
意味が通じた？
├─ YES → メッセージを「修正済み」表示（色変更）
│         LLM に送信 → レスポンス追加
│
└─ NO  → 「意味が通じません」メッセージ表示
         次の入力を待機
```

## 実装コンポーネント

### 1. RecognizedInputBuffer（C:\src_dotnet\Robin\Services\RecognizedInputBuffer.cs）

**目的**: 音声認識結果のバッファリングとウォッチドッグ処理

**主要機能**:
- `AddRecognition(string)`: 認識結果をバッファに追加
- `GetBuffer()`: 現在のバッファ内容を取得
- `FlushBuffer()`: バッファをクリアして内容を返す
- `IsEmpty`: バッファが空かどうかを判定

**イベント**:
- `BufferReady`: バッファがウォッチドッグのタイムアウトで処理準備完了時に発火
- `BufferUpdated`: バッファが更新されたときに発火
- `BufferCleared`: バッファがクリアされたときに発火

**タイムアウト**: 2秒（カスタマイズ可能）

```csharp
// 使用例
var buffer = new RecognizedInputBuffer(timeoutMs: 2000);
buffer.BufferReady += (sender, content) => {
    // バッファ処理実行
    Console.WriteLine($"バッファ準備完了: {content}");
};

buffer.AddRecognition("こんに");  // 最初の認識
buffer.AddRecognition("ちは");    // 2番目の認識
// 2秒後: BufferReady イベント発火 → "こんにちは"
```

### 2. SemanticValidationService（C:\src_dotnet\Robin\Services\SemanticValidationService.cs）

**目的**: 音声認識テキストの意味妥当性を LLM で判定し、誤認識を修正

**主要メソッド**:
- `ValidateAsync(string recognizedText)`: 音声認識テキストの意味妥当性を判定（非同期）

**返り値**: `SemanticValidationResult`

```csharp
public class SemanticValidationResult
{
    public bool IsSemanticValid { get; set; }        // 意味が通じるか
    public string CorrectedText { get; set; }        // 修正後テキスト
    public string Feedback { get; set; }             // LLMからのフィードバック
    public DateTime ValidatedAt { get; set; }        // 検証時刻
}
```

**LLM リクエストのプロンプト**:

LLM に以下の形式で JSON を返すよう指示：

```json
{
  "isSemanticValid": boolean,
  "correctedText": "修正後のテキスト",
  "feedback": "簡潔な理由"
}
```

**音声認識誤認識の考慮**:
- 同音異義語（例: 「せんせい」→「先生」or「先制」）
- 音声認識による誤認識（例: 「あいみゃく」→「愛脈」or「相ミャク」）

### 3. Message モデルの拡張（C:\src_dotnet\Robin\Models\Message.cs）

**新規追加フィールド**:

```csharp
public class Message
{
    // ... 既存フィールド ...

    /// 音声認識結果の元のテキスト（修正前）
    public string? OriginalRecognizedText { get; set; }

    /// 意味妥当性判定結果
    public SemanticValidationResult? SemanticValidation { get; set; }

    /// メッセージの表示状態
    public MessageDisplayState DisplayState { get; set; }

    /// 実際に表示するコンテンツを返す
    public string GetDisplayContent() =>
        SemanticValidation?.IsSemanticValid == true ?
            (SemanticValidation.CorrectedText ?? Content) :
            Content;

    /// 意味検証が適用されているか
    public bool IsSemanticValidationApplied =>
        SemanticValidation?.IsSemanticValid == true;
}
```

**表示状態の種類**:

```csharp
public enum MessageDisplayState
{
    Final,                    // 最終的なメッセージ
    RawRecognized,           // 生の音声認識結果を表示中
    ValidatingSemantics,     // 意味妥当性を判定中
    SemanticValidated        // 意味解析済み（修正完了）
}
```

### 4. MessageAdapter の更新（C:\src_dotnet\Robin\Adapters\MessageAdapter.cs）

**2段階表示への対応**:

1. **生の認識結果**: デフォルト色（黒）で表示
2. **検証中**: オレンジ色で表示
3. **意味検証済み（修正完了）**: 緑色で表示

**新規メソッド**:

```csharp
public void UpdateMessage(int position, Message message)
{
    // メッセージをその場で更新（色変更を反映）
    if (position >= 0 && position < _messages.Count)
    {
        _messages[position] = message;
        NotifyItemChanged(position);
    }
}
```

**UIの色設定**:

```csharp
if (message.IsSemanticValidationApplied)
{
    _messageText.SetTextColor(Color.ParseColor("#4CAF50")); // 緑
}
else if (message.DisplayState == MessageDisplayState.ValidatingSemantics)
{
    _messageText.SetTextColor(Color.ParseColor("#FF9800")); // オレンジ
}
else
{
    _messageText.SetTextColor(Color.ParseColor("#212121")); // デフォルト黒
}
```

### 5. ConversationService の拡張（C:\src_dotnet\Robin\Services\ConversationService.cs）

**新規メソッド**:

```csharp
/// 指定インデックスのメッセージに意味検証結果を反映
public void UpdateMessageWithSemanticValidation(int messageIndex, SemanticValidationResult validationResult)

/// 最後のユーザーメッセージを取得
public Message? GetLastUserMessage()

/// 最後のユーザーメッセージのインデックスを取得
public int GetLastUserMessageIndex()
```

### 6. MainActivity への統合（C:\src_dotnet\Robin\MainActivity.cs）

**新規フィールド**:

```csharp
private RecognizedInputBuffer? _inputBuffer;
private SemanticValidationService? _semanticValidationService;
```

**初期化**:

```csharp
private void InitializeServices()
{
    // バッファの初期化
    _inputBuffer = new RecognizedInputBuffer(timeoutMs: 2000);
    _inputBuffer.BufferReady += OnInputBufferReady;

    // ... 他のサービス初期化 ...

    // セマンティック検証サービスの初期化
    _semanticValidationService = new SemanticValidationService(_openAIService);
}
```

**イベントハンドラー**:

#### `OnRecognitionResult` / `OnSherpaFinalResult`（修正）

```csharp
private void OnSherpaFinalResult(object? sender, string recognizedText)
{
    // バッファに認識結果を追加（ウォッチドッグで処理される）
    _inputBuffer?.AddRecognition(recognizedText);

    RunOnUiThread(() =>
    {
        _statusText!.Text = "入力待機中...";
        _statusText!.Visibility = ViewStates.Visible;
    });
}
```

#### `OnInputBufferReady`（新規）

バッファ準備完了時の処理フロー：

1. **ユーザーメッセージを追加**（生の認識結果）
2. **意味妥当性を判定**（LLMを呼び出し）
3. **意味が通じた場合**:
   - メッセージを更新（修正済みテキストと色変更）
   - LLMに送信して応答を取得
4. **意味が通じない場合**:
   - ユーザーに通知
   - 次の入力を待機

```csharp
private async void OnInputBufferReady(object? sender, string bufferContent)
{
    // ユーザーメッセージを追加
    RunOnUiThread(() =>
    {
        _conversationService?.AddUserMessage(bufferContent);
        _statusText!.Text = "意味を判定中...";
        _statusText!.Visibility = ViewStates.Visible;
    });

    // 意味妥当性を判定
    var validationResult = await _semanticValidationService.ValidateAsync(bufferContent);

    if (validationResult.IsSemanticValid)
    {
        // メッセージを更新して LLM処理へ
        var messageIndex = _conversationService?.GetLastUserMessageIndex() ?? -1;
        _conversationService?.UpdateMessageWithSemanticValidation(messageIndex, validationResult);
        _messageAdapter?.UpdateMessage(messageIndex, ...);

        // LLM処理
        await ProcessValidInput(validationResult.CorrectedText ?? bufferContent);
    }
    else
    {
        // 意味が通じない場合、次の入力を待つ
        RunOnUiThread(() =>
        {
            _statusText!.Text = "意味が通じません。もう一度お願いします。";
            // 3秒後に非表示
            _statusText!.PostDelayed(() =>
            {
                _statusText!.Visibility = ViewStates.Gone;
            }, 3000);
        });
    }
}
```

## シーケンス図

```
ユーザー      MainActivity    Buffer      LLM Service    MessageAdapter
  │                │             │              │              │
  │ 話しかける      │             │              │              │
  ├─────────────→  │             │              │              │
  │                │ AddRecognition │          │              │
  │                ├─────────────→ │          │              │
  │                │             │ ウォッチドッグ発火  │              │
  │                │ ←─────────── │          │              │
  │                │ BufferReady  │          │              │
  │ 認識結果表示   │ AddUserMessage          │              │
  │ （黒色）      ├──────────────────────────────────────→  │
  │                │             │              │ 追加        │
  │                │ ValidateAsync │          │              │
  │                ├──────────────────────→  │              │
  │                │             │ JSON判定  │              │
  │ 修正済み表示  │ ←─────────────────────  │              │
  │ （緑色）      │ UpdateMessage           │              │
  │                ├──────────────────────────────────────→  │
  │                │             │              │ 更新+色変更 │
  │                │ SendMessageAsync          │              │
  │                ├──────────────────────→  │              │
  │                │             │ LLM処理  │              │
  │ AI応答表示    │ ←─────────────────────  │              │
  │                │ AddAssistantMessage      │              │
  │                ├──────────────────────────────────────→  │
  │                │             │              │ 追加        │
```

## 使用例

### 基本的な使用フロー

1. **ユーザーが音声入力**
   ```
   ユーザー: 「こんにちは」
   ```

2. **音声認識結果がバッファに蓄積**
   ```
   認識1: 「こん」
   認識2: 「にち」
   認識3: 「は」
   ```

3. **2秒後、ウォッチドッグが発火**
   ```
   バッファ内容: 「こんにちは」
   ```

4. **LLMが意味を判定**
   ```
   入力: 「こんにちは」
   判定: IsSemanticValid = true
   修正: CorrectedText = 「こんにちは」
   フィードバック: 「標準的な挨拶として認識」
   ```

5. **画面更新**
   ```
   黒色 「こんにちは」 → 緑色 「こんにちは」（修正なし）
   ```

6. **LLMに送信**
   ```
   LLMレスポンス: 「こんにちは。何かお手伝いできることはありますか？」
   ```

### 誤認識修正の例

1. **誤認識が発生**
   ```
   ユーザー: 「タスクを作成して」
   認識結果: 「タス苦を作成して」
   ```

2. **LLMが修正**
   ```
   入力: 「タス苦を作成して」
   判定: IsSemanticValid = false → true（修正後）
   修正: CorrectedText = 「タスクを作成して」
   フィードバック: 「音声認識誤り。『タスク』に修正」
   ```

3. **画面更新**
   ```
   黒色 「タス苦を作成して」 → 緑色 「タスクを作成して」
   ```

## エラーハンドリング

### LLM検証エラー時

LLM検証に失敗した場合、元のテキストで処理を継続：

```csharp
try
{
    var validationResult = await _semanticValidationService.ValidateAsync(bufferContent);
    // ...
}
catch (Exception ex)
{
    // エラー時は元のテキストで処理
    await ProcessValidInput(bufferContent);
}
```

### 検証サービスが初期化されていない場合

```csharp
if (_semanticValidationService == null)
{
    // バッファリング機能なしで直接処理
    await ProcessValidInput(bufferContent);
    return;
}
```

## パフォーマンス考慮

### バッファのタイムアウト

- **デフォルト**: 2秒
- **調整方法**:
  ```csharp
  _inputBuffer = new RecognizedInputBuffer(timeoutMs: 3000); // 3秒に変更
  ```

### バッファリングによる遅延

- バッファリングにより 2秒の遅延が発生
- 利点: 複数の認識セグメントを統合できる
- 欠点: レスポンスタイムが増加

### LLM呼び出しの最適化

- 意味判定は簡潔な JSON 形式で実装
- タイムアウト: 60秒（OpenAIService の設定）

## テスト方法

### 手動テスト

1. **基本動作テスト**
   - マイクボタンを押して音声入力
   - 認識結果がバッファに蓄積されることを確認
   - 2秒後に意味判定が実行されることを確認

2. **意味判定テスト**
   - 意味の通じるテキストを入力
   - 画面上で色が黒から緑に変わることを確認

3. **誤認識修正テスト**
   - 誤認識が修正されることを確認
   - 修正後のテキストで LLM が応答することを確認

4. **意味不明テスト**
   - 意味の通じないテキストを入力
   - 「意味が通じません」メッセージが表示されることを確認
   - 次の入力待機状態になることを確認

### ログ確認

```csharp
// MainActivity で以下のログを確認
Android.Util.Log.Info("MainActivity", $"バッファ準備完了: {bufferContent}");
Android.Util.Log.Info("SemanticValidationService", $"意味判定完了: Valid={result.IsSemanticValid}");
```

## 今後の改善案

1. **バッファタイムアウトの動的調整**
   - ユーザーの話速に応じて自動調整

2. **キャッシング機能**
   - 同じテキストの意味判定結果をキャッシュ

3. **音声認識の連続処理**
   - 複数のセンテンスを自動分割して処理

4. **ユーザーオプション**
   - 意味検証の有効/無効をユーザーが切り替え可能

5. **言語別の調整**
   - 言語ごとに異なる検証プロンプトを使用

## トラブルシューティング

### バッファが発火しない

**原因**: ウォッチドッグのタイムアウトが短すぎる可能性

**解決策**:
```csharp
_inputBuffer = new RecognizedInputBuffer(timeoutMs: 3000); // タイムアウトを増加
```

### LLM 検証が遅い

**原因**: LLM API の応答時間が長い、またはネットワーク遅延

**解決策**:
- OpenAIService のタイムアウト値を調整
- ローカル LLM（LM Studio）の使用を検討

### 意味判定が正確でない

**原因**: LLM の判定プロンプトが適切でない

**解決策**:
- SemanticValidationService の `BuildValidationPrompt()` を調整
- より詳細なプロンプトを提供

## まとめ

本実装により、以下が実現されます：

1. **ユーザーフレンドリー**: 認識結果が段階的に改善される様子が視覚的に示される
2. **誤認識対応**: 音声認識の誤認識が自動的に修正される
3. **信頼性**: 意味の通じないテキストは LLM 処理前に検出される
4. **拡張性**: 新しい言語や検証ロジックを容易に追加できる

