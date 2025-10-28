# システムプロンプト クイックリファレンス

## 2 つのプロンプト

### 1. Conversation プロンプト（デフォルト）

**用途**：通常の会話、ラバーダックデバッグ

**特徴**：
- 親しみやすい
- 思考整理を支援
- 一緒に考えるパートナー

**自動使用**：通常のチャット応答時

### 2. SemanticValidation プロンプト

**用途**：音声認識結果の意味検証と修正

**特徴**：
- 同音異義語を修正
- JSON で結果を返す
- isSemanticValid, correctedText, feedback を含む

**自動使用**：RecognizedInputBuffer → SemanticValidationService での自動切り替え

## 使用方法

### プロンプトを明示的に設定

```csharp
// Conversation に切り替え
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);

// SemanticValidation に切り替え
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);

// カスタムプロンプトで設定
_openAIService.SetSystemPrompt("独自のプロンプトテキスト");
```

### 現在のプロンプトを確認

```csharp
string currentPrompt = _openAIService.GetSystemPrompt();
```

### セマンティック検証時の自動切り替え（SemanticValidationService 内で自動）

```csharp
// SemanticValidationService.ValidateAsync() 内部で自動処理
var originalPrompt = _openAIService.GetSystemPrompt();
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
// LLM 処理...
_openAIService.SetSystemPrompt(originalPrompt);  // 自動復帰
```

## よく使う方法

### ケース 1: 通常の会話回答を生成する

```csharp
// デフォルトのまま（Conversation が自動で使用される）
var response = await _openAIService.SendMessageAsync(messages);
```

### ケース 2: カスタムプロンプトで特別な応答を生成する

```csharp
_openAIService.SetSystemPrompt("あなたは日本語教師です...");
var response = await _openAIService.SendMessageAsync(messages);

// 必要に応じて元に戻す
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);
```

### ケース 3: エラーハンドリング付き

```csharp
var originalPrompt = _openAIService.GetSystemPrompt();

try
{
    _openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
    // 処理...
}
finally
{
    _openAIService.SetSystemPrompt(originalPrompt);  // 確実に復帰
}
```

## API リクエスト内容

### セットされるメッセージ構造

```json
[
  {
    "role": "system",
    "content": "あなたはRobinという名前のAIアシスタントです..."
  },
  {
    "role": "user",
    "content": "ユーザーのメッセージ"
  },
  {
    "role": "assistant",
    "content": "AI の前回の応答"
  }
]
```

## ログ出力

### プロンプト変更時のログ

```
[OpenAIService] システムプロンプトを変更: Conversation
[OpenAIService] システムプロンプトを変更: SemanticValidation
[OpenAIService] システムプロンプトをカスタム文字列で設定
```

### デバッグ用ログ

```csharp
Log.Info("Debug", $"Current Prompt: {_openAIService.GetSystemPrompt().Substring(0, 100)}");
```

## 新しいプロンプトタイプの追加

### 3 ステップで追加可能

**1. SystemPrompts.cs にプロンプト定数を追加**

```csharp
public const string CodingAssistantPrompt = @"
あなたはコーディングアシスタントです...
";
```

**2. PromptType enum に新しい値を追加**

```csharp
public enum PromptType
{
    Conversation,
    SemanticValidation,
    CodingAssistant  // ← 新規
}
```

**3. GetSystemPrompt() メソッドに対応ケースを追加**

```csharp
public static string GetSystemPrompt(PromptType promptType)
{
    return promptType switch
    {
        PromptType.CodingAssistant => CodingAssistantPrompt,  // ← 新規
        PromptType.Conversation => ConversationSystemPrompt,
        PromptType.SemanticValidation => SemanticValidationSystemPrompt,
        _ => ConversationSystemPrompt
    };
}
```

**4. 使用**

```csharp
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.CodingAssistant);
```

## 一般的な間違い

❌ **避けるべき**

```csharp
// プロンプトを毎回文字列で設定（一貫性が失われる）
_openAIService.SetSystemPrompt("何か別の指示...");

// 復帰処理なしでプロンプト変更（後続の処理に影響）
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
// プロンプットを戻さずに終了！
```

✅ **推奨**

```csharp
// プリセットを使用
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);

// 必ず復帰処理を実装
var originalPrompt = _openAIService.GetSystemPrompt();
try
{
    _openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
    // 処理...
}
finally
{
    _openAIService.SetSystemPrompt(originalPrompt);
}
```

## トークン見積もり

| プロンプト | トークン数 | 用途 |
|-----------|-----------|------|
| Conversation | 約 200～300 | 通常の会話 |
| SemanticValidation | 約 300～400 | 音声認識修正 |

## 参考資料

- **完全ガイド**: `claudedocs/system-prompts-guide.md`
- **技術仕様**: `claudedocs/system-prompts-implementation.md`
- **実装概要**: `claudedocs/SYSTEM-PROMPTS-SUMMARY.md`

## よくある質問

**Q: デフォルトのプロンプトは何ですか？**
A: Conversation プロンプトです。OpenAIService 初期化時に自動設定されます。

**Q: SemanticValidation の切り替えは自動ですか？**
A: はい。SemanticValidationService が内部で自動的に処理します。

**Q: プロンプトを永続化できますか？**
A: 現在は実装されていません。将来 Settings に保存する機能を追加予定です。

**Q: 複数のカスタムプロンプトを管理できますか？**
A: 現在は最後に SetSystemPrompt() で設定した内容が有効です。複数管理したい場合は拡張が必要です。

**Q: API 呼び出しのたびにプロンプトが送信されますか？**
A: はい。各 API リクエストに system メッセージとして含められます。

## トラブルシューティング

**問題**: AI の応答が期待と異なる
**確認**: `Log.Info("Debug", _openAIService.GetSystemPrompt());` で現在のプロンプトを確認

**問題**: JSON パースエラー（意味検証）
**確認**: SemanticValidationService.ParseValidationResponse() のログを確認

**問題**: プロンプト復帰が失敗した
**確認**: SemanticValidationService の try-finally ブロックでエラーハンドリングされているか確認
