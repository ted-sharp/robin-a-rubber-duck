# システムプロンプト実装 - 完了サマリー

## 実装内容

### 1. システムプロンプトの定義

2 種類のシステムプロンプトを作成し、LLM の動作を目的に応じて制御できるようにしました。

#### 通常の会話用プロンプト（Conversation）

Robin が親しみやすいラバーダック型のパートナーとして機能します：

- **特徴**：丁寧で親友のような会話、思考整理の支援、段階的な説明
- **用途**：プログラミング相談、技術的な問題、日常的な悩み相談
- **応答スタイル**：2～5 段落の自然な会話、必要に応じてコード例を含める

**例**：
```
ユーザー: 「非同期処理がうまくいきません」
Robin: 「つまり、期待した結果が得られないということですね。
具体的には、どのような問題が起きていますか？」
```

#### 意味検証用プロンプト（SemanticValidation）

音声認識の誤認識を修正し、意味妥当性を判定します：

- **特徴**：同音異義語の修正、助詞の補正、文法チェック
- **用途**：音声認識結果の自動修正
- **出力形式**：JSON（isSemanticValid, correctedText, feedback）

**例**：
```
入力：「タス苦を作成して」
出力：{
  "isSemanticValid": true,
  "correctedText": "タスクを作成して",
  "feedback": "音声認識誤り：'苦'を'ク'に修正"
}
```

### 2. 実装ファイル

#### 新規作成

**`Models/SystemPrompts.cs`** (7.2 KB)
```csharp
public static class SystemPrompts
{
    // プロンプト定数
    public const string ConversationSystemPrompt = ...
    public const string SemanticValidationSystemPrompt = ...

    // プロンプトタイプ指定
    public enum PromptType { Conversation, SemanticValidation }

    // プロンプト取得メソッド
    public static string GetSystemPrompt(PromptType promptType)
}
```

#### 修正ファイル

**`Services/OpenAIService.cs`**

追加内容：
- `_systemPrompt` フィールド（デフォルト：Conversation）
- `SetSystemPrompt(PromptType)` メソッド
- `SetSystemPrompt(string)` メソッド
- `GetSystemPrompt()` メソッド
- `BuildRequest()` メソッドを修正して system メッセージを自動追加

変更：API リクエストに system ロールのメッセージを最初に挿入

**`Services/SemanticValidationService.cs`**

修正内容：
- `ValidateAsync()` メソッド内でプロンプト切り替え処理を実装
  1. 現在のプロンプトを保存
  2. SemanticValidation プロンプトに切り替え
  3. LLM 処理実行
  4. 元のプロンプトに復帰

- エラー時も最低限 Conversation プロンプトに復帰
- `BuildValidationPrompt()` メソッドを簡潔化

### 3. ドキュメント

#### `claudedocs/system-prompts-guide.md` (11 KB)

ユーザー向けの包括的なガイド：

- システムプロンプトの概要と役割
- 2 種類のプロンプトの詳細説明
- 使用例とコード例
- カスタマイズ方法
- デバッグ方法
- ベストプラクティス
- トラブルシューティング

#### `claudedocs/system-prompts-implementation.md` (11 KB)

開発者向けの技術仕様書：

- ファイル構成と変更箇所
- メソッド実装の詳細
- プロンプント内容の詳細解説
- API リクエストフロー
- トークン使用量への影響
- デバッグポイント
- セキュリティ考慮事項
- 将来の拡張方法

## 実装の特徴

### ✅ 柔軟性

```csharp
// プロンプトタイプで指定
openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);
openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);

// カスタム文字列でも指定可能
openAIService.SetSystemPrompt("独自のプロンプト...");
```

### ✅ 安全性

```csharp
// セマンティック検証時の自動切り替え
var originalPrompt = _llmService.GetSystemPrompt();
_llmService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
// 処理...
_llmService.SetSystemPrompt(originalPrompt);  // 確実に復帰
```

### ✅ 拡張性

新しいプロンプトタイプを追加するのが簡単：

```csharp
// 1. 新しいプロンプト定数を定義
public const string CustomPrompt = @"...";

// 2. PromptType enum に追加
public enum PromptType { Conversation, SemanticValidation, Custom }

// 3. GetSystemPrompt() に対応ケースを追加
PromptType.Custom => CustomPrompt,
```

### ✅ 互換性

- 既存の API は変わらない（デフォルトで Conversation プロンプト使用）
- 段階的に導入可能
- 既存のコード変更は不要

## 処理フロー

### 通常の会話フロー

```
ユーザー入力
├─ 音声認識
├─ RecognizedInputBuffer で一時保存
└─ 2 秒タイムアウト後...
   ├─ SemanticValidationService.ValidateAsync()
   │  ├─ プロンプト保存
   │  ├─ SemanticValidation プロンプトに切り替え
   │  ├─ LLM で意味検証
   │  └─ Conversation プロンプトに復帰
   └─ ProcessValidInput()
      ├─ Conversation プロンプムで応答生成
      └─ AI メッセージを表示
```

## テスト推奨項目

1. **通常会話**
   - プログラミング質問への応答スタイル確認
   - ラバーダック的な対応確認

2. **意味検証**
   - 同音異義語の修正確認：「タス苦」→「タスク」
   - 助詞の補正確認：「です」「ですね」
   - 意味不明な入力の検出：「あぎじばすがぷますぎす」

3. **プロンプト切り替え**
   - エラー時のプロンプト復帰確認
   - ログ出力の確認

4. **パフォーマンス**
   - トークン使用量の確認
   - API レスポンス時間の確認

## ビルド状態

✅ **ビルド成功**

```
ビルドに成功しました。
0 個の警告
0 エラー
```

すべてのコンパイルエラーなし。

## ファイルの場所

| ファイル | 説明 | サイズ |
|---------|------|--------|
| `Models/SystemPrompts.cs` | プロンプト定義 | 7.2 KB |
| `Services/OpenAIService.cs` | LLM サービス（修正） | - |
| `Services/SemanticValidationService.cs` | 意味検証（修正） | - |
| `claudedocs/system-prompts-guide.md` | ユーザーガイド | 11 KB |
| `claudedocs/system-prompts-implementation.md` | 技術仕様 | 11 KB |

## 次のステップ

### 短期（すぐに実施可能）

1. **テスト**
   - 実際の会話でプロンプトの動作確認
   - 音声認識結果の修正品質確認

2. **調整**
   - プロンプント内容の微調整（必要に応じて）
   - パフォーマンス最適化

### 中期（将来の機能追加）

1. **カスタマイズ機能**
   - ユーザーが Settings からプロンプトをカスタマイズ可能に

2. **新しいプロンプトタイプ**
   - コーディングアシスタント専用プロンプト
   - 日本語教師用プロンプト
   - など

3. **プロンプント管理**
   - プロンプントのバージョン管理
   - A/B テスト機能

## まとめ

Robin のシステムプロンプト機能により、以下が実現されました：

✅ **2 種類のプリセットプロンプト**
- 通常会話：ラバーダックデバッグ型
- 意味検証：音声認識誤認識修正

✅ **柔軟な切り替え機構**
- プロンプトタイプまたはカスタム文字列で設定
- 安全な復帰処理で一貫性を保証

✅ **拡張可能な設計**
- 新しいプロンプトタイプを簡単に追加可能
- 将来のカスタマイズに対応

✅ **包括的なドキュメント**
- ユーザーガイド
- 技術仕様書

これにより、Robin の応答が目的に応じて最適化され、より自然で効果的なユーザー体験が実現されます。
