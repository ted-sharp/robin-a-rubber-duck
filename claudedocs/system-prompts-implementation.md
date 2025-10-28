# システムプロンプト実装仕様

## ファイル構成

### 新規作成ファイル

#### `Models/SystemPrompts.cs`
システムプロンプトの定義とアクセスメソッドを提供するスタティッククラス

**内容**
- `ConversationSystemPrompt` (定数)：通常の会話用プロンプト
- `SemanticValidationSystemPrompt` (定数)：意味検証用プロンプト
- `PromptType` (enum)：プロンプトタイプの指定
- `GetSystemPrompt(PromptType)` (静的メソッド)：プロンプトを取得

### 修正ファイル

#### `Services/OpenAIService.cs`

**追加フィールド**
```csharp
private string _systemPrompt = SystemPrompts.GetSystemPrompt(
    SystemPrompts.PromptType.Conversation
);
```

デフォルトは Conversation プロンプトで初期化されます。

**追加メソッド**

1. `SetSystemPrompt(SystemPrompts.PromptType promptType)`
   - プロンプトタイプを指定して設定
   - ログ出力：`システムプロンプトを変更: {promptType}`

2. `SetSystemPrompt(string customPrompt)`
   - カスタム文字列でプロンプトを設定
   - ログ出力：`システムプロンプトをカスタム文字列で設定`

3. `GetSystemPrompt()`
   - 現在のシステムプロンプトを返す
   - 戻り値：`string _systemPrompt`

**修正メソッド：BuildRequest()**

```csharp
private HttpContent BuildRequest(List<Message> conversationHistory)
{
    var apiMessages = new List<ApiMessage>
    {
        // システムプロンプトを最初に追加
        new ApiMessage
        {
            Role = "system",
            Content = _systemPrompt
        }
    };

    // 会話履歴を追加
    apiMessages.AddRange(conversationHistory.Select(m => new ApiMessage
    {
        Role = m.Role == MessageRole.User ? "user" : "assistant",
        Content = m.Content
    }));

    // リクエストの構築...
}
```

変更点：
- API メッセージリストの最初に system ロールのメッセージを追加
- 現在の `_systemPrompt` を使用

#### `Services/SemanticValidationService.cs`

**修正メソッド：ValidateAsync()**

```csharp
public async Task<SemanticValidationResult> ValidateAsync(string recognizedText)
{
    if (_llmService == null)
    {
        // 既存のエラーハンドリング...
    }

    try
    {
        // 1. 現在のプロンプトを保存
        var originalPrompt = _llmService.GetSystemPrompt();

        // 2. 意味検証用プロンプトに切り替え
        _llmService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);

        // 3. LLM にリクエスト
        var prompt = BuildValidationPrompt(recognizedText);
        var messages = new List<Message>
        {
            new Message
            {
                Role = MessageRole.User,
                Content = prompt,
                DisplayState = MessageDisplayState.Final
            }
        };

        var response = await _llmService.SendMessageAsync(messages);

        // 4. 元のシステムプロンプトに戻す
        _llmService.SetSystemPrompt(originalPrompt);

        // 5. レスポンス処理...
    }
    catch (Exception ex)
    {
        // エラーハンドリング
        try
        {
            var originalPrompt = _llmService.GetSystemPrompt();
            _llmService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);
        }
        catch
        {
            // 復帰失敗時も処理を続行
        }

        return new SemanticValidationResult
        {
            IsSemanticValid = true,
            CorrectedText = recognizedText,
            Feedback = $"判定エラー: {ex.Message}"
        };
    }
}
```

変更点：
- 意味検証前に現在のプロンプトを保存
- SemanticValidation プロンプトに切り替え
- LLM 処理後、元のプロンプトに戻す
- エラー時も、最低限 Conversation プロンプトに復帰

**修正メソッド：BuildValidationPrompt()**

```csharp
private string BuildValidationPrompt(string recognizedText)
{
    return $@"以下の音声認識結果の意味妥当性を判定し、必要に応じて修正してください。

音声認識結果: ""{recognizedText}""";
}
```

簡潔化理由：
- システムプロンプト（SemanticValidationSystemPrompt）に詳細な指示が含まれているため
- プロンプトの重複を避ける
- トークン使用量を削減

## システムプロンプトの内容

### 1. ConversationSystemPrompt

**キー要素**

```
あなたはRobinという名前のAIアシスタントです。
```

**基本的な姿勢**
- 親しみやすく、丁寧な日本語
- 話を聞いて理解しようとする姿勢
- 段階的な説明
- 不確かな情報は前置きする

**プログラミング相談時**
- 詳しく、初心者にも分かるように
- ベストプラクティスを示す
- メリット・デメリットを説明
- 現在のコンテキストを考慮

**ラバーダック的対応**
- 質問を通じて思考を整理
- 「つまり～ですね」という確認
- ユーザーが自分で気づけるような質問
- 一緒に考えるパートナー

**応答の長さ**
- 通常2～5段落
- 必要に応じてコードスニペット
- リストや箇条書きで読みやすく

### 2. SemanticValidationSystemPrompt

**キー要素**

```
音声認識の結果を分析し、意味的な妥当性を判定し、
音声認識の誤りを修正する専門家です。
```

**判定基準**

意味が通じる（isSemanticValid: true）
- 日本語として文法的に正しい
- 実在する言葉や概念
- 文の意図が明確

意味が通じない（isSemanticValid: false）
- 造語や不可解な単語の組み合わせ
- 文法的に成立していない
- 音声認識の失敗が明らか

**修正戦略**
- 同音異義語や類似音の修正
- 助詞や助動詞の補正
- 文脈を考慮した修正

**出力形式**
```json
{
  "isSemanticValid": boolean,
  "correctedText": "修正後のテキスト",
  "feedback": "簡潔な理由"
}
```

## API リクエストの流れ

### 従来（プロンプトなし）

```
Message 1: role=user, content="こんにちは"
Message 2: role=assistant, content="..."
Message 3: role=user, content="次の質問"
```

### 更新後（プロンプトあり）

```
Message 1: role=system, content="RobinというAIアシスタントです。..."
Message 2: role=user, content="こんにちは"
Message 3: role=assistant, content="..."
Message 4: role=user, content="次の質問"
```

追加：最初に system ロールのメッセージを挿入

## トークン使用量への影響

### Conversation プロンプト
- プロンプント内容：約 200～300 トークン
- 典型的な会話：プロンプト + ユーザー + AI レスポンス = 合計 700～1000 トークン

### SemanticValidation プロンプト
- プロンプント内容：約 300～400 トークン（より詳細）
- 検証リクエスト：プロンプト + JSON スキーマ説明 = 合計 400～600 トークン

## 既存コードとの互換性

### OpenAIService

✅ **互換性あり**
- 既存の SendMessageAsync() メソッドのシグネチャは変わらない
- デフォルトで Conversation プロンプトが使用される
- プロンプト切り替えは省略可能（オプション）

### SemanticValidationService

✅ **互換性あり**
- ValidateAsync() のシグネチャは変わらない
- プロンプト管理は内部で自動処理

### 既存の会話フロー

```
MainActivity.OnInputBufferReady()
└─ SemanticValidationService.ValidateAsync()
   └─ LLM プロンプト自動切り替え（内部処理）
└─ ProcessValidInput()
   └─ LLM 通常プロンプト（自動復帰）
```

## デバッグ時の確認ポイント

### 1. プロンプト設定の確認

```csharp
Log.Info("Debug", _openAIService.GetSystemPrompt().Substring(0, 100));
```

### 2. API リクエストの確認

OpenAI API ダッシュボードでリクエスト内容を確認：
- Message 1 の role が "system" であることを確認
- Content に期待するプロンプントが含まれていることを確認

### 3. ログ出力の確認

```
[OpenAIService] システムプロンプトを変更: SemanticValidation
[SemanticValidationService] 意味判定完了: Valid=true, Corrected=タスクを作成して
[OpenAIService] システムプロンプトを変更: Conversation
```

## セキュリティ考慮事項

### プロンプト インジェクション対策

SetSystemPrompt(string) でカスタム文字列を設定する場合、入力をバリデーションしてください：

```csharp
public void SetSystemPrompt(string customPrompt)
{
    if (string.IsNullOrWhiteSpace(customPrompt))
    {
        throw new ArgumentException("プロンプットが空です");
    }

    if (customPrompt.Length > 10000) // 制限例
    {
        throw new ArgumentException("プロンプットが長すぎます");
    }

    _systemPrompt = customPrompt;
}
```

## 将来の拡張

### 新しいプロンプトタイプの追加例

```csharp
// 1. SystemPrompts.cs を修正
public const string CodingAssistantPrompt = @"...";

public enum PromptType
{
    Conversation,
    SemanticValidation,
    CodingAssistant  // 新規
}

public static string GetSystemPrompt(PromptType promptType)
{
    return promptType switch
    {
        PromptType.CodingAssistant => CodingAssistantPrompt,
        // ...既存...
    };
}

// 2. 使用方法
openAIService.SetSystemPrompt(SystemPrompts.PromptType.CodingAssistant);
```

### ユーザー設定可能なプロンプト

将来の機能として、ユーザーが Settings から プロンプトをカスタマイズできるようにすることも可能です：

```csharp
// SettingsActivity で保存
settingsService.SaveSystemPrompt("custom_id", customPromptText);

// MainActivity で読み込み
var customPrompt = settingsService.LoadSystemPrompt("custom_id");
openAIService.SetSystemPrompt(customPrompt);
```

## まとめ

システムプロンプト実装は以下を実現します：

✅ **柔軟性**：2 種類のプリセット + カスタムプロンプト対応
✅ **安全性**：プロンプト切り替え後の復帰処理を確実に実装
✅ **拡張性**：新しいプロンプトタイプを簡単に追加可能
✅ **トークン効率**：プロンプント内容を最適化
✅ **互換性**：既存コードへの影響を最小化
