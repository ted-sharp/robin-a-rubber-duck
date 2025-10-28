# テキスト修正と LLM 統合ガイド

## 概要

このドキュメントは、音声認識テキストの修正が LLM にどのように統合されるかについて説明しています。修正済みのテキストが画面に表示され、同時に LLM への入力として使用されます。

## フロー図

```
音声認識結果（修正前）
    ↓
バッファに蓄積
    ↓
ウォッチドッグ発火
    ↓
【意味検証フェーズ】
LLM に「このテキストは意味が通じるか？」と問い合わせ
    ↓
    ├─ YES → テキストを修正（必要に応じて）
    │
    └─ NO → ユーザー通知、次の入力待機
    ↓
【UI 表示フェーズ】
修正済みテキストを画面に表示
（色: 緑=検証OK、青=修正実施）
    ↓
【LLM 処理フェーズ】
【画面に表示されたテキスト】を会話履歴に含める
    ↓
LLM に送信
    ↓
LLM のレスポンスを相手のメッセージとして表示
```

## 実装詳細

### 1. テキスト修正フロー

#### ステップ 1: 意味検証

```csharp
// SemanticValidationService でテキストを検証
var validationResult = await _semanticValidationService.ValidateAsync(bufferContent);

// 結果例:
// {
//   "isSemanticValid": true,
//   "correctedText": "修正後のテキスト",
//   "feedback": "修正理由"
// }
```

#### ステップ 2: メッセージの更新

```csharp
// ConversationService でメッセージを更新
_conversationService?.UpdateMessageWithSemanticValidation(
    messageIndex,
    validationResult
);
```

**内部処理**:
```csharp
public void UpdateMessageWithSemanticValidation(int messageIndex, SemanticValidationResult validationResult)
{
    var message = _messages[messageIndex];
    message.SemanticValidation = validationResult;

    // 意味が通じた場合、Content を修正済みテキストで更新
    if (validationResult.IsSemanticValid && !string.IsNullOrEmpty(validationResult.CorrectedText))
    {
        message.Content = validationResult.CorrectedText;  // ← LLM に渡されるテキスト
    }

    SaveMessagesToStorage();
}
```

#### ステップ 3: UI 更新

```csharp
// MessageAdapter で色と内容を更新
_messageAdapter?.UpdateMessage(messageIndex, message);
```

**表示ルール**:
- **緑色**: 意味検証OK、修正なし
- **青色**: 意味検証OK、修正実施
- **オレンジ**: 検証中
- **黒色**: 通常のメッセージ（検証なし）

#### ステップ 4: LLM に送信

```csharp
// 会話履歴を取得（修正済みテキストが含まれている）
var messages = _conversationService.GetMessages();

// 最後のユーザーメッセージを確認
var lastUserMessage = _conversationService.GetLastUserMessage();

// Content フィールドに修正済みテキストが格納されている
// → この内容が LLM に送信される
var response = await _openAIService.SendMessageAsync(messages);
```

### 2. Message クラスの修正情報

#### 新規フィールド

```csharp
public class Message
{
    // ... 既存フィールド ...

    /// 修正前の音声認識テキスト
    public string? OriginalRecognizedText { get; set; }

    /// LLM からの修正結果
    public SemanticValidationResult? SemanticValidation { get; set; }

    /// 修正が行われたかどうか
    public bool WasCorrected =>
        IsSemanticValidationApplied &&
        !string.IsNullOrEmpty(OriginalRecognizedText) &&
        OriginalRecognizedText != Content;

    /// 修正内容の詳細（例: "修正: xxx → yyy"）
    public string GetCorrectionDetails();

    /// 修正情報を含む詳細内容
    public string GetDetailedContent();
}
```

#### 使用例

```csharp
// 修正情報をログに出力
var message = _conversationService.GetLastUserMessage();

if (message.WasCorrected)
{
    Console.WriteLine(message.GetCorrectionDetails());
    // 出力例: 修正: "タス苦を作成して" → "タスクを作成して"
}

// 詳細内容を取得（修正情報を含む）
Console.WriteLine(message.GetDetailedContent());
// 出力例:
// タスクを作成して
// [修正: "タス苦を作成して" → "タスクを作成して"]
```

### 3. ログ出力による追跡

ProcessValidInput メソッドでは、修正情報を詳細に記録します：

```
[LLM への入力テキスト: 'タスクを作成して' (修正前: 'タス苦を作成して')]
[テキスト修正実行: 'タス苦を作成して' → 'タスクを作成して']
[LLM からのレスポンス取得: ...]
```

## 使用例

### 例 1: 誤認識が修正される場合

```
ユーザー: 「タスクを作成して」

【音声認識】
認識結果: 「タス苦を作成して」

【バッファリング】
バッファ内容: 「タス苦を作成して」

【意味検証】
入力: 「タス苦を作成して」
判定: IsSemanticValid = false（意味不明）
修正: CorrectedText = 「タスクを作成して」
フィードバック: 「『苦』を『ク』に修正」

【UI 表示】
色: 青色（修正実施）
内容:
  タスクを作成して
  [修正: "タス苦を作成して" → "タスクを作成して"]

【LLM 処理】
会話履歴: [修正済みテキスト: 「タスクを作成して」]
LLM レスポンス:
  「タスクを作成しました。以下の項目が追加されました...」
```

### 例 2: 誤認識がない場合

```
ユーザー: 「こんにちは」

【音声認識】
認識結果: 「こんにちは」

【バッファリング】
バッファ内容: 「こんにちは」

【意味検証】
入力: 「こんにちは」
判定: IsSemanticValid = true（標準的な挨拶）
修正: CorrectedText = 「こんにちは」（修正なし）

【UI 表示】
色: 緑色（検証OK、修正なし）
内容: こんにちは

【LLM 処理】
会話履歴: [テキスト: 「こんにちは」]
LLM レスポンス:
  「こんにちは。何かお手伝いできることはありますか？」
```

### 例 3: 意味が通じない場合

```
ユーザー: 「あぎじばすがぷますぎす」（ノイズで認識不能）

【音声認識】
認識結果: 「あぎじばすがぷますぎす」

【バッファリング】
バッファ内容: 「あぎじばすがぷますぎす」

【意味検証】
入力: 「あぎじばすがぷますぎす」
判定: IsSemanticValid = false（意味不明）
修正: 失敗（修正案なし）

【UI 表示】
ステータス: 「意味が通じません。もう一度お願いします。」
（3秒後に非表示）

【LLM 処理】
実行されない（次の入力を待機）
```

## コード追跡例

### ProcessValidInput メソッドの実行フロー

```csharp
private async Task ProcessValidInput(string validatedText)
{
    // 1. LLM のレスポンスステータス表示
    _statusText.Text = "レスポンス待機中...";

    // 2. 会話履歴を取得
    var messages = _conversationService.GetMessages();

    // 3. 最後のユーザーメッセージを確認
    var lastUserMessage = _conversationService.GetLastUserMessage();

    // 4. ログに修正情報を記録
    if (lastUserMessage != null)
    {
        Android.Util.Log.Info("MainActivity",
            $"LLM への入力テキスト: '{lastUserMessage.Content}' " +
            $"(修正前: '{lastUserMessage.OriginalRecognizedText ?? lastUserMessage.Content}')");

        // 修正が行われた場合
        if (lastUserMessage.SemanticValidation?.IsSemanticValid == true &&
            lastUserMessage.OriginalRecognizedText != null &&
            lastUserMessage.OriginalRecognizedText != lastUserMessage.Content)
        {
            Android.Util.Log.Info("MainActivity",
                $"テキスト修正実行: '{lastUserMessage.OriginalRecognizedText}' → " +
                $"'{lastUserMessage.Content}'");
        }
    }

    // 5. LLM に送信（修正済みテキストを含む会話履歴）
    var response = await _openAIService.SendMessageAsync(messages);
    //                                    ^^^^^^^^
    //     ここで修正済みテキストが LLM に送信される

    // 6. ログに LLM レスポンスを記録
    Android.Util.Log.Info("MainActivity",
        $"LLM からのレスポンス取得: {response.Substring(0, Math.Min(100, response.Length))}...");

    // 7. AI メッセージとして追加
    _conversationService.AddAssistantMessage(response);
}
```

## UI の色分け

| 色 | 意味 | 表示内容 |
|-----|------|---------|
| **緑** (#4CAF50) | 意味検証OK、修正なし | 元のテキストそのまま |
| **青** (#2196F3) | 意味検証OK、修正実施 | 修正後のテキスト + 修正内容 |
| **オレンジ** (#FF9800) | 検証中 | 生のテキスト |
| **黒** (#212121) | 検証なし（AI メッセージなど） | 元のテキスト |

## ログ出力の確認方法

Android Studio の Logcat で以下のタグで検索：
```
MainActivity
SemanticValidationService
```

### 出力例

```
[MainActivity] バッファ準備完了: タス苦を作成して
[SemanticValidationService] 意味判定完了: Valid=True, Corrected=タスクを作成して
[MainActivity] LLM への入力テキスト: 'タスクを作成して' (修正前: 'タス苦を作成して')
[MainActivity] テキスト修正実行: 'タス苦を作成して' → 'タスクを作成して'
[MainActivity] LLM からのレスポンス取得: タスクを作成しました...
```

## まとめ

### キーポイント

1. **修正前後が記録される**
   - `OriginalRecognizedText`: 修正前の音声認識結果
   - `Content`: 修正済みテキスト（LLM に渡される）

2. **修正済みテキストが LLM に送信される**
   - `_conversationService.GetMessages()` で取得した会話履歴に、修正済みテキストが含まれている
   - この会話履歴が `_openAIService.SendMessageAsync()` に送信される

3. **UI で修正過程が可視化される**
   - 生のテキスト（黒）→ 修正済みテキスト（青）→ AI レスポンス
   - ユーザーが修正の過程を確認できる

4. **ログで追跡可能**
   - すべての修正操作がログに記録される
   - デバッグやトラブルシューティングが容易

## トラブルシューティング

### 修正されていない？

**確認項目**:
1. LLM の回答が `isSemanticValid = true` か確認
2. `CorrectedText` が空でないか確認
3. ログで修正メッセージが出力されているか確認

```
[MainActivity] テキスト修正実行: ... → ...
```

このログが出力されていなければ修正されていません。

### LLM に修正前のテキストが渡されている？

**確認方法**:
ログで以下をチェック：
```
[MainActivity] LLM への入力テキスト: '○○' (修正前: '△△')
```

修正前と後が異なっていれば、正しく修正済みテキストが LLM に渡されています。

## 参考資料

- [実装ドキュメント](semantic-validation-buffer-implementation.md)
- Message クラス: `src_dotnet/Robin/Models/Message.cs`
- MessageAdapter: `src_dotnet/Robin/Adapters/MessageAdapter.cs`
- MainActivity: `src_dotnet/Robin/MainActivity.cs`
