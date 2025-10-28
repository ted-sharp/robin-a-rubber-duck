# Robin システムプロンプトガイド

## 概要

Robin では、LLM の動作を制御するために 2 種類のシステムプロンプトを用意しています：

1. **通常の会話用プロンプト** - ラバーダックデバッグ型の相談相手
2. **意味検証用プロンプト** - 音声認識の誤認識修正と意味妥当性判定

## システムプロンプトの役割

### システムプロンプトとは

システムプロンプトは、LLM に対して「あなたはどのような AI か」「どのように応答すべきか」を指示する基本的なガイドラインです。OpenAI の Chat API では、最初のメッセージとして「system」ロールで送信します。

## 1. 通常の会話用プロンプト（Conversation）

### 目的

ユーザーとの自然な会話を通じて、プログラミング、技術的な問題、日常的な悩みなどを相談できるパートナーとしての役割を果たします。

### 主な特徴

#### 親しみやすさ
- 丁寧で自然な日本語で応答
- 親友のような気軽な態度
- 理解しようとする姿勢を示す

#### ラバーダック的な対応
- ユーザーの話を聞いて思考を整理するのを支援
- 質問を通じた確認対応
- ユーザーが自分で解決策に気づけるようなサポート

#### プログラミング相談の際の対応
- コードを詳しく説明
- ベストプラクティスを示す
- 複数の解決方法がある場合はメリット・デメリットを説明
- 現在のコンテキストを考慮

#### 応答の長さ
- 通常は 2～5 段落
- 必要に応じてコードスニペットを含める
- リストや箇条書きで読みやすくする

### 使用例

```
ユーザー: 「非同期処理がうまくいきません」

Robin: 「非同期処理がうまくいかないですか。つまり、期待した結果が得られないということですね。
具体的には、どのような問題が起きていますか？例えば、値が返されないのか、順序がおかしいのか、
それとも別のエラーが出ているのか教えていただけますか？」
```

## 2. 意味検証用プロンプト（SemanticValidation）

### 目的

音声認識結果の意味妥当性を判定し、同音異義語や音声認識エラーによる誤認識を修正します。

### 主な特徴

#### 判定対象

**意味が通じる場合（isSemanticValid: true）**
- 日本語として文法的に正しい
- 実在する言葉や概念
- 文の意図が明確
- 例：「タスクを作成して」「会議の時間は？」

**意味が通じない場合（isSemanticValid: false）**
- 造語や不可解な単語の組み合わせ
- 文法的に成立していない
- 音声認識の失敗が明らか
- 例：「あぎじばすがぷますぎす」

#### 修正戦略

**同音異義語や類似音の修正**
- 「タス苦」→「タスク」（苦 vs ク）
- 「トウキョウ」→「東京」vs「東京都」

**助詞や助動詞の補正**
- 「作成して」と「作成したら」の判別
- 文末の自動補完

**文脈を考慮した修正**
- プログラミング用語に偏る傾向を考慮
- 複数の修正可能性がある場合は最も一般的なものを選択
- 疑わしい場合は元のテキストを保持

### 出力形式

JSON 形式で以下の構造を返却：

```json
{
  "isSemanticValid": boolean,
  "correctedText": "修正後のテキスト（修正不要の場合は元のテキスト）",
  "feedback": "修正内容や判定理由（簡潔に）"
}
```

### 使用例

#### 例 1: 同音異義語の修正

**入力**
```
タス苦を作成して
```

**出力**
```json
{
  "isSemanticValid": true,
  "correctedText": "タスクを作成して",
  "feedback": "音声認識誤り：'苦'を'ク'に修正"
}
```

#### 例 2: 意味不明

**入力**
```
あぎじばすがぷますぎす
```

**出力**
```json
{
  "isSemanticValid": false,
  "correctedText": "あぎじばすがぷますぎす",
  "feedback": "意味不明。マイク音量を上げるか、もう一度話し直してください"
}
```

#### 例 3: 修正不要

**入力**
```
メール送ってください
```

**出力**
```json
{
  "isSemanticValid": true,
  "correctedText": "メール送ってください",
  "feedback": "修正不要"
}
```

## プロンプトの切り替え実装

### コード例

```csharp
// システムプロンプトを設定（プロンプトタイプで指定）
openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);
openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);

// カスタムプロンプトで設定
openAIService.SetSystemPrompt("カスタムプロンプトテキスト");

// 現在のシステムプロンプトを取得
string currentPrompt = openAIService.GetSystemPrompt();
```

### SemanticValidationService での実装

SemanticValidationService は、意味検証時に自動的にプロンプトを切り替えます：

```csharp
// 現在のプロンプトを保存
var originalPrompt = _llmService.GetSystemPrompt();

// 意味検証用プロンプトに切り替え
_llmService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);

// LLM にリクエスト
var response = await _llmService.SendMessageAsync(messages);

// 元のプロンプトに戻す
_llmService.SetSystemPrompt(originalPrompt);
```

## システムプロンプトのライフサイクル

### 初期化時

```
MainActivity.java
↓
OpenAIService 初期化
↓
_systemPrompt = SystemPrompts.GetSystemPrompt(
    SystemPrompts.PromptType.Conversation
)
```

デフォルトは「Conversation」プロンプトで初期化されます。

### 通常の会話フロー

```
1. ユーザーが入力
2. 音声認識
3. RecognizedInputBuffer で一時保存
4. SemanticValidationService で検証
   └─ 意味検証用プロンプトに切り替え
   └─ JSON レスポンスを取得
   └─ 元のプロンプトに戻す
5. ProcessValidInput で AI レスポンス生成
   └─ Conversation プロンプトで処理
```

## パフォーマンス考慮事項

### トークン使用量

**Conversation プロンプト**
- 約 200～300 トークン
- 1 回のリクエストで平均 500 トークン消費

**SemanticValidation プロンプト**
- 約 300～400 トークン（より詳細な指示）
- 1 回のリクエストで平均 400 トークン消費

### 最適化

- システムプロンプトはキャッシュできるプロバイダーもあります
- 意味検証は必要な場合のみ実行して、トークン使用量を削減できます

## カスタマイズ方法

### プロンプトの変更

SystemPrompts.cs で定義されている `ConversationSystemPrompt` または `SemanticValidationSystemPrompt` を修正します：

```csharp
public static class SystemPrompts
{
    public const string ConversationSystemPrompt = @"
        // カスタマイズ済みプロンプト
    ";
}
```

### 新しいプロンプトタイプの追加

1. PromptType enum に新しい値を追加
2. GetSystemPrompt メソッドに対応するケースを追加
3. プロンプト定数を定義

```csharp
public enum PromptType
{
    Conversation,
    SemanticValidation,
    CustomType  // 新規
}

public static string GetSystemPrompt(PromptType promptType)
{
    return promptType switch
    {
        // ...既存...
        PromptType.CustomType => CustomTypeSystemPrompt,
        _ => ConversationSystemPrompt
    };
}
```

## デバッグ方法

### ログ出力

システムプロンプトの変更はすべて Logcat に出力されます：

```
[OpenAIService] システムプロンプトを変更: SemanticValidation
[OpenAIService] システムプロンプトをカスタム文字列で設定
```

### API リクエストの確認

OpenAIService.BuildRequest() は API リクエストボディに system メッセージを含めるため、実際のプロンプトが正しく送信されていることを確認できます。

## ベストプラクティス

### 1. プロンプトの一貫性

同じ用途で複数のプロンプトを使用しないでください：

```csharp
// 良い例
openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);

// 避けるべき例
openAIService.SetSystemPrompt("適当な指示文字列");
```

### 2. プロンプトの復帰処理

プロンプトを一時的に変更する場合は、必ず復帰処理を実装してください：

```csharp
var originalPrompt = openAIService.GetSystemPrompt();
try
{
    openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
    // 処理実行
}
finally
{
    openAIService.SetSystemPrompt(originalPrompt);
}
```

### 3. エラーハンドリング

プロンプト関連のエラーでも、元のプロンプトへの復帰を確実にしてください。

## トラブルシューティング

### AI レスポンスが期待と異なる

**原因**：システムプロンプトが正しく設定されていない可能性があります。

**確認方法**
```csharp
Log.Info("Debug", openAIService.GetSystemPrompt());
```

### 意味検証の結果が不正確

**原因**：SemanticValidation プロンプトの指示が不適切な可能性があります。

**改善方法**
1. フィードバック文字列を確認
2. 複数の例でテスト
3. プロンプト文を修正

### JSON パース エラー

**原因**：LLM が正しい JSON を返していない可能性があります。

**対策**
1. SemanticValidation プロンプトで「JSONのみ出力」の指示を強化
2. 例を増やす
3. LLM モデルを変更（より安定したモデルを使用）

## まとめ

Robin のシステムプロンプトシステムは、以下の機能を提供します：

✅ **2 種類のプリセットプロンプト**：通常会話と意味検証に特化
✅ **柔軟な切り替え**：プロンプトタイプまたはカスタム文字列で設定可能
✅ **安全な復帰処理**：エラー時も元のプロンプトに戻せる
✅ **拡張性**：新しいプロンプトタイプを簡単に追加可能

このシステムにより、Robin の応答を目的に応じて最適化できます。
