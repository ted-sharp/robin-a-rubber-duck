# システムプロンプト設定機能

## 概要

Robin のシステムプロンプト（Conversation と SemanticValidation）を**画面から動的に変更**でき、設定を**設定ファイルから読み込む**ことができるようになりました。

## 機能

### 1. 画面からのプロンプット変更

**メニューから「システムプロンプト設定」を選択** → システムプロンプト設定画面を開く

**画面で以下を設定可能：**
- ☑ カスタムプロンプトを使用（チェックボックス）
- Conversation プロンプット（テキスト編集）
- SemanticValidation プロンプット（テキスト編集）

**各プロンプトの「デフォルトにリセット」ボタン**で初期値に戻す

### 2. 設定ファイルからの読み込み

JSON 形式の設定ファイルに以下を追記可能：

```json
{
  "systemPromptSettings": {
    "conversationPrompt": "あなたはRobinという...",
    "semanticValidationPrompt": "音声認識の結果を...",
    "useCustomPrompts": true
  }
}
```

設定ファイルをインポート → 設定が自動適用される

### 3. 起動時の自動適用

アプリ起動時に保存されているシステムプロンプト設定を自動読み込み＆適用

## ファイル構成

### 新規作成

#### UI・レイアウト

- **`Resources/layout/dialog_system_prompts.xml`** (2.8 KB)
  - システムプロンプト設定画面のレイアウト
  - スクロール対応
  - 両プロンプットのテキスト編集エリア

- **`Resources/drawable/edit_text_border.xml`**
  - EditText のボーダースタイル

#### Activity

- **`SystemPromptsActivity.cs`** (3.5 KB)
  - システムプロンプト設定画面の Activity
  - プロンプット読み込み・保存
  - デフォルトリセット機能

### 修正

#### モデル

**`Models/Configuration.cs`**
- `SystemPromptSettings` クラスを追加
  - `ConversationPrompt` (string?)
  - `SemanticValidationPrompt` (string?)
  - `UseCustomPrompts` (bool)

#### サービス

**`Services/SettingsService.cs`**

追加メソッド：
- `SaveSystemPromptSettings(SystemPromptSettings)` - 設定保存
- `LoadSystemPromptSettings()` - 設定読み込み
- `ClearSystemPromptSettings()` - 設定クリア

追加機能：
- `ApplyConfiguration()` メソッドを拡張して systemPromptSettings に対応

#### UI

**`Resources/menu/drawer_menu.xml`**
- `nav_system_prompts` メニュー項目を追加

**`MainActivity.cs`**
- `ApplySystemPromptSettings()` メソッドを追加
  - 起動時に保存されているプロンプット設定を読み込んで OpenAIService に適用
- ドロワー ナビゲーション に `nav_system_prompts` ハンドラを追加
  - SystemPromptsActivity を起動

## 使用フロー

### 画面から変更する場合

```
アプリ起動
  ↓
[ドロワーメニュー] → [システムプロンプト設定]
  ↓
SystemPromptsActivity を開く
  ↓
☑ カスタムプロンプトを使用 (チェック)
  ↓
EditText でプロンプトを編集
  ↓
[保存] ボタンをタップ
  ↓
SharedPreferences に保存
  ↓
次回起動時に自動適用
```

### 設定ファイルから読み込む場合

```
JSON 設定ファイル作成
  ├── llmSettings
  ├── sttSettings
  └── systemPromptSettings ← ここに追加
      ├── conversationPrompt
      ├── semanticValidationPrompt
      └── useCustomPrompts

アプリ内で [設定ファイルをインポート]
  ↓
SettingsService.ApplyConfiguration()
  ↓
systemPromptSettings が自動読み込み＆適用
```

## API 仕様

### SystemPromptSettings クラス

```csharp
public class SystemPromptSettings
{
    // Conversation プロンプット（null で保存されていない）
    public string? ConversationPrompt { get; set; }

    // SemanticValidation プロンプット
    public string? SemanticValidationPrompt { get; set; }

    // カスタムプロンプトを使用するかどうか
    public bool UseCustomPrompts { get; set; } = false;

    // コンストラクタ
    public SystemPromptSettings();
    public SystemPromptSettings(string? conversationPrompt,
                                string? semanticValidationPrompt,
                                bool useCustomPrompts);
}
```

### SettingsService メソッド

#### SaveSystemPromptSettings

```csharp
public void SaveSystemPromptSettings(SystemPromptSettings settings)
```

- `conversationPrompt` を `SharedPreferences` に保存
- `semanticValidationPrompt` を保存
- `useCustomPrompts` フラグを保存
- ログ出力：`システムプロンプト設定を保存（カスタムプロンプト使用: true/false）`

#### LoadSystemPromptSettings

```csharp
public SystemPromptSettings LoadSystemPromptSettings()
```

- 保存されているプロンプット設定を読み込む
- デフォルト：`useCustomPrompts = false`
- 空の場合は null で返す

#### ClearSystemPromptSettings

```csharp
public void ClearSystemPromptSettings()
```

- すべてのシステムプロンプト設定をクリア

### MainActivity.ApplySystemPromptSettings

```csharp
private void ApplySystemPromptSettings()
```

- SettingsService からプロンプット設定を読み込む
- `useCustomPrompts = true` の場合、カスタムプロンプットを OpenAIService に設定
- `useCustomPrompts = false` の場合、デフォルト Conversation プロンプントを設定
- 設定適用時にログ出力

## SharedPreferences キー

| キー | 説明 | 値の型 |
|------|------|--------|
| `conversation_prompt` | Conversation プロンプット | String |
| `semantic_validation_prompt` | SemanticValidation プロンプット | String |
| `use_custom_prompts` | カスタムプロンプット使用フラグ | Boolean |

## 設定ファイルの例

### config-sample.json

```json
{
  "llmSettings": {
    "provider": "lm-studio",
    "endpoint": "http://192.168.0.7:1234",
    "modelName": "openai/gpt-oss-20b",
    "isEnabled": true
  },
  "sttSettings": {
    "provider": "sherpa-onnx",
    "language": "ja",
    "isEnabled": true
  },
  "systemPromptSettings": {
    "conversationPrompt": "あなたはRobinという名前のAIアシスタントです。...",
    "semanticValidationPrompt": "音声認識の結果を分析し、意味的な妥当性を判定し、...",
    "useCustomPrompts": true
  }
}
```

## ログ出力

### プロンプット保存時

```
[SettingsService] システムプロンプト設定を保存（カスタムプロンプト使用: true）
```

### プロンプット適用時（起動時）

```
[MainActivity] カスタム Conversation プロンプットを適用
```

または

```
[MainActivity] デフォルト Conversation プロンプットを適用
```

### 設定ファイルから読み込み時

```
[SettingsService] システムプロンプト設定を適用しました
```

## 実装の特徴

### ✅ 動的変更対応

実行中にプロンプットを変更 → アプリを再起動で新しいプロンプットが反映される

### ✅ 設定ファイル対応

JSON 設定ファイルに systemPromptSettings を含める → インポート時に自動適用

### ✅ デフォルト値

カスタムプロンプットが設定されていない場合は、SystemPrompts クラスのデフォルトプロンプットを使用

### ✅ 画面 UI

システムプロンプト設定画面から直感的に変更可能

## 使用例

### コード例 1: プログラムで設定を変更

```csharp
// SettingsService インスタンス作成
var settingsService = new SettingsService(context);

// カスタムプロンプットを設定
var settings = new SystemPromptSettings(
    conversationPrompt: "あなたはコーディングアシスタントです。",
    semanticValidationPrompt: "音声認識結果を...修正してください。",
    useCustomPrompts: true
);

settingsService.SaveSystemPromptSettings(settings);
```

### コード例 2: 設定ファイルから適用

```csharp
// 設定ファイルを読み込み
var config = await settingsService.LoadConfigurationFromFileAsync(filePath);

// 設定を適用
if (config != null)
{
    settingsService.ApplyConfiguration(config);
}
```

## トラブルシューティング

### Q: 画面から設定を変更してもアプリに反映されない

**A**: アプリを再起動してください。起動時に `ApplySystemPromptSettings()` が呼ばれるため。

または、MainActivity で以下を呼び出す：

```csharp
ApplySystemPromptSettings();
```

### Q: 設定ファイルから systemPromptSettings が読み込まれない

**A**: 以下を確認：

1. JSON 形式が正しいか（特にキー名の大文字小文字）
2. systemPromptSettings セクションが正しくネストされているか
3. SettingsService.ApplyConfiguration() が呼ばれているか

### Q: デフォルトプロンプットに戻したい

**A**: 画面で各プロンプットの「デフォルトにリセット」ボタンをタップ

### Q: プロンプット設定をクリアしたい

**A**: コード内から:

```csharp
settingsService.ClearSystemPromptSettings();
```

## テスト推奨項目

1. **画面からの変更**
   - [ ] プロンプット編集画面を開ける
   - [ ] テキストを編集できる
   - [ ] デフォルトリセットボタンで戻る
   - [ ] 保存後、アプリ再起動で反映される

2. **設定ファイルからの読み込み**
   - [ ] JSON に systemPromptSettings セクションを追加
   - [ ] インポートで設定が読み込まれる
   - [ ] アプリが反映されたプロンプットで動作する

3. **カスタムプロンプット使用フラグ**
   - [ ] useCustomPrompts = false で、デフォルトプロンプットが使用される
   - [ ] useCustomPrompts = true で、カスタムプロンプットが使用される

4. **ログ確認**
   - [ ] SettingsService のログが正しく出力される
   - [ ] MainActivity の適用ログが出力される

## 将来の拡張

1. **ホットリロード**
   - アプリ再起動なしに、プロンプットの変更をリアルタイム反映

2. **プロンプットプリセット**
   - 複数のプロンプットセットを保存・切り替え

3. **AI プロンプット生成**
   - LLM にプロンプット生成を依頼して自動作成

4. **プロンプット検証**
   - 新しいプロンプットをテストして品質を評価

## ビルド・デプロイ

### ビルド状態

✅ **成功**

```
ビルドに成功しました。
46 個の警告
0 エラー
```

### デプロイ

1. アプリをビルド・インストール
2. メニュー → システムプロンプト設定 で UI を確認
3. プロンプットを編集・保存してテスト

## まとめ

このシステムプロンプト設定機能により、以下が実現されます：

✅ **動的カスタマイズ** - 画面からプロンプットを変更可能
✅ **ファイル対応** - 設定ファイルから自動読み込み
✅ **直感的な UI** - SystemPromptsActivity で簡単に編集
✅ **段階的な適用** - 起動時に自動反映

Robin のプロンプットを、用途に応じて柔軟にカスタマイズできるようになりました。
