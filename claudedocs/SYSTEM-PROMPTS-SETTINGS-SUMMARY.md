# システムプロンプト設定機能 - 完了サマリー

## ✅ 実装完了

Robin のシステムプロンプト（Conversation と SemanticValidation）を、**画面から動的に変更**でき、**設定ファイルから読み込める**ようになりました。

## 🎯 実装内容

### 1. データモデル

**`Models/Configuration.cs`** に `SystemPromptSettings` クラスを追加：

```csharp
public class SystemPromptSettings
{
    public string? ConversationPrompt { get; set; }           // Conversation プロンプット
    public string? SemanticValidationPrompt { get; set; }    // SemanticValidation プロンプット
    public bool UseCustomPrompts { get; set; } = false;      // カスタムプロンプット使用フラグ
}
```

### 2. サービス層

**`Services/SettingsService.cs`** に以下を追加：

```csharp
// プロンプット設定を保存
public void SaveSystemPromptSettings(SystemPromptSettings settings)

// プロンプット設定を読み込む
public SystemPromptSettings LoadSystemPromptSettings()

// プロンプット設定をクリア
public void ClearSystemPromptSettings()

// 設定ファイルから自動適用
ApplyConfiguration() メソッドを拡張
```

### 3. UI・画面

#### SystemPromptsActivity.cs（新規）
- プロンプット設定画面の Activity
- テキスト編集
- デフォルトリセット機能

#### レイアウト
- **`Resources/layout/dialog_system_prompts.xml`**
  - スクロール対応のシステムプロンプト設定画面
  - 2 つのプロンプット編集エリア
  - リセット・保存ボタン

- **`Resources/drawable/edit_text_border.xml`**
  - EditText ボーダースタイル

#### ナビゲーション
- **`Resources/menu/drawer_menu.xml`** を更新
  - 「システムプロンプト設定」メニュー項目を追加

### 4. 起動時の自動適用

**`MainActivity.cs`** に以下を追加：

```csharp
// 起動時に保存されているプロンプット設定を読み込んで適用
private void ApplySystemPromptSettings()
```

実行フロー：
```
OnCreate()
  ↓
InitializeServices()
  ↓
ApplySystemPromptSettings()  ← プロンプット設定を自動読み込み＆適用
  ↓
AppReady
```

## 📂 ファイル一覧

### 新規作成

| ファイル | 説明 | サイズ |
|---------|------|--------|
| `SystemPromptsActivity.cs` | プロンプット設定画面 | 3.5 KB |
| `Resources/layout/dialog_system_prompts.xml` | 設定画面レイアウト | 2.8 KB |
| `Resources/drawable/edit_text_border.xml` | EditText スタイル | 0.3 KB |
| `claudedocs/system-prompts-settings.md` | 設定機能ドキュメント | 11 KB |
| `claudedocs/config-system-prompts-example.json` | 設定ファイル例 | 4 KB |

### 修正ファイル

| ファイル | 変更箇所 |
|---------|--------|
| `Models/Configuration.cs` | `SystemPromptSettings` クラス追加 |
| `Services/SettingsService.cs` | プロンプット管理メソッド 3 個追加 |
| `MainActivity.cs` | `ApplySystemPromptSettings()` メソッド追加、ナビゲーション拡張 |
| `Resources/menu/drawer_menu.xml` | メニュー項目追加 |

## 🔄 動作フロー

### 画面からの変更

```
[アプリ起動]
  ↓
[ドロワーメニュー] → [システムプロンプト設定]
  ↓
[SystemPromptsActivity]
  ├─ ☑ カスタムプロンプトを使用
  ├─ [Conversation プロンプット編集]
  ├─ [SemanticValidation プロンプット編集]
  └─ [保存]
  ↓
[SharedPreferences に保存]
  ↓
[アプリ再起動]
  ↓
[起動時に自動適用]
```

### 設定ファイルからの適用

```
[JSON 設定ファイル]
  └─ systemPromptSettings セクション
      ├─ conversationPrompt
      ├─ semanticValidationPrompt
      └─ useCustomPrompts

[アプリ内でインポート]
  ↓
[SettingsService.ApplyConfiguration()]
  ↓
[自動読み込み＆適用]
  ↓
[次回起動時に有効]
```

## 🎯 使用例

### 設定ファイル例（config-system-prompts-example.json）

```json
{
  "llmSettings": { ... },
  "sttSettings": { ... },
  "systemPromptSettings": {
    "conversationPrompt": "あなたはRobin...",
    "semanticValidationPrompt": "音声認識の結果を...",
    "useCustomPrompts": true
  }
}
```

### コード例（プログラム側）

```csharp
// 設定を保存
var settings = new SystemPromptSettings(
    "カスタム Conversation プロンプット",
    "カスタム SemanticValidation プロンプット",
    useCustomPrompts: true
);
settingsService.SaveSystemPromptSettings(settings);

// 設定を読み込み
var loaded = settingsService.LoadSystemPromptSettings();

// 設定ファイルから適用
var config = await settingsService.LoadConfigurationFromFileAsync(path);
settingsService.ApplyConfiguration(config);
```

## 📊 SharedPreferences キー

| キー | 説明 | 値の型 |
|------|------|--------|
| `conversation_prompt` | Conversation プロンプット | String |
| `semantic_validation_prompt` | SemanticValidation プロンプット | String |
| `use_custom_prompts` | カスタムプロンプット使用フラグ | Boolean |

## ✨ 実装の特徴

### ✅ 動的カスタマイズ
- 画面から いつでも プロンプットを変更可能
- 設定は SharedPreferences に永続保存

### ✅ ファイル対応
- JSON 設定ファイルから自動読み込み
- systemPromptSettings セクションをインポート時に処理

### ✅ 自動適用
- アプリ起動時に保存されているプロンプット設定を自動読み込み
- `ApplySystemPromptSettings()` で OpenAIService に反映

### ✅ デフォルト値対応
- カスタムプロンプットが設定されていない場合は SystemPrompts クラスのデフォルトを使用
- いつでも「デフォルトにリセット」でリセット可能

### ✅ 直感的な UI
- SystemPromptsActivity で簡単に編集
- チェックボックスでカスタムプロンプット有効/無効切り替え

## 📖 ドキュメント

### system-prompts-settings.md
- 完全なドキュメント
- API 仕様詳細
- トラブルシューティング
- テスト推奨項目

### config-system-prompts-example.json
- 設定ファイルの使用例
- 実際のプロンプント内容を含む
- JSON スキーマを示す

## 🔍 ログ出力例

### 保存時
```
[SettingsService] システムプロンプト設定を保存（カスタムプロンプト使用: true）
[SystemPromptsActivity] システムプロンプト設定を保存しました (Toast)
```

### 起動時
```
[MainActivity] カスタム Conversation プロンプットを適用
```

または

```
[MainActivity] デフォルト Conversation プロンプットを適用
```

### 設定ファイル適用時
```
[SettingsService] システムプロンプト設定を適用しました
```

## 🛠️ ビルド状態

✅ **成功**

```
ビルドに成功しました。
46 個の警告
0 エラー
```

すべてのコンパイルエラーなし。

## 🧪 テスト推奨項目

### 画面からの変更
- [ ] 「システムプロンプト設定」メニュー項目が表示される
- [ ] SystemPromptsActivity が開く
- [ ] プロンプットを編集できる
- [ ] 「デフォルトにリセット」ボタンで初期値に戻る
- [ ] 「保存」ボタンで設定が保存される
- [ ] アプリ再起動で新しいプロンプットが反映される

### 設定ファイルからの読み込み
- [ ] JSON に systemPromptSettings セクションを追加
- [ ] 「設定ファイルをインポート」で読み込まれる
- [ ] システムプロンプット設定が自動適用される
- [ ] アプリ再起動時に反映される

### カスタムプロンプット使用フラグ
- [ ] useCustomPrompts = false → デフォルトプロンプット使用
- [ ] useCustomPrompts = true → カスタムプロンプット使用

## 📋 チェックリスト

実装完了項目：

- ✅ Configuration に SystemPromptSettings を追加
- ✅ SettingsService にプロンプット管理メソッドを追加
- ✅ SystemPromptsActivity を実装
- ✅ UI レイアウトを作成
- ✅ ナビゲーションメニューに項目を追加
- ✅ MainActivity で起動時の自動適用を実装
- ✅ 設定ファイル対応を実装
- ✅ 包括的なドキュメントを作成
- ✅ 設定ファイル例を作成
- ✅ ビルド成功（0 エラー）

## 🚀 次のステップ

### 短期（推奨）

1. **テスト実施**
   - 画面からのプロンプット変更を確認
   - 設定ファイルからの読み込みを確認

2. **ログ確認**
   - LogCat でプロンプット設定ログを確認

### 中期（将来機能）

1. **ホットリロード**
   - アプリ再起動なしでプロンプット変更を反映

2. **プロンプットプリセット**
   - 複数のプロンプットセットを保存・切り替え

3. **プロンプット検証**
   - 新しいプロンプットをテスト実行して品質を評価

4. **プロンプット履歴**
   - 変更履歴を記録・復元

## 📝 まとめ

このシステムプロンプト設定機能により、Robin は以下を実現します：

✅ **完全なカスタマイズ** - 画面とファイルから独立してプロンプットを変更可能
✅ **継続性** - 設定は SharedPreferences に永続保存
✅ **自動適用** - 起動時に自動的に設定が反映
✅ **使いやすさ** - 直感的な UI で簡単に変更可能

Robin のプロンプットを、ユースケースに応じて柔軟にカスタマイズできるようになりました！

---

**実装日**: 2025-10-28
**ビルド状態**: ✅ 成功
**ドキュメント完成度**: 100%
