# システムプロンプト機能 - 完全ガイド

## 📚 ドキュメント インデックス

Robin のシステムプロンプト機能に関するすべてのドキュメント。

### クイックリファレンス

**すぐに使いたい** → [`SYSTEM-PROMPTS-QUICK-REFERENCE.md`](SYSTEM-PROMPTS-QUICK-REFERENCE.md)
- 2 つのプロンプット概要
- よく使うコード例
- よくある質問

---

## 🎓 詳細ドキュメント

### 1. システムプロンプント定義と使用方法

📄 **`system-prompts-guide.md`** - 完全ガイド
- システムプロンプトの概念
- 2 つのプロンプントの詳細説明
- API リクエストフロー
- ベストプラクティス
- トラブルシューティング

**読むべき人**: システムプロンプションの仕組みを詳しく理解したい開発者

---

### 2. 実装の技術仕様

📄 **`system-prompts-implementation.md`** - 技術仕様書
- ファイル構成
- メソッド実装詳細
- API 仕様
- トークン使用量
- デバッグポイント
- セキュリティ考慮事項

**読むべき人**: 実装の詳細を知りたい開発者

---

### 3. 設定機能（画面・ファイル対応）

📄 **`system-prompts-settings.md`** - 設定機能完全ドキュメント
- 画面からの動的変更
- 設定ファイルからの読み込み
- SystemPromptsActivity の使用方法
- SharedPreferences キー一覧
- API 仕様
- 設定ファイル JSON スキーマ

**読むべき人**:
- プロンプット設定画面を使いたい
- 設定ファイルで自動適用したい
- 設定機能をカスタマイズしたい

**該当ファイル**:
- `SystemPromptsActivity.cs` (3.5 KB)
- `Resources/layout/dialog_system_prompts.xml`
- `Services/SettingsService.cs` (プロンプット管理メソッド)
- `Models/Configuration.cs` (SystemPromptSettings クラス)

---

## 📊 サマリードキュメント

### 実装概要

📄 **`SYSTEM-PROMPTS-SUMMARY.md`** - 基本実装のサマリー
- 実装内容の概要
- ファイル一覧
- 処理フロー
- テスト推奨項目
- ビルド状態

**読むべき人**: 実装の全体像を素早く把握したい

---

### 設定機能の概要

📄 **`SYSTEM-PROMPTS-SETTINGS-SUMMARY.md`** - 設定機能のサマリー
- 画面からのプロンプット変更
- 設定ファイルからの読み込み
- 起動時の自動適用
- 動作フロー
- ビルド状態

**読むべき人**: 画面・ファイル設定機能の全体像を知りたい

---

## 📐 このドキュメント

📄 **このファイル** - 全機能のインデックスと関連図
- すべてのドキュメントへのリンク
- 機能の説明と対応ドキュメント
- 読むべき順序の推奨

---

## 🗂️ 設定ファイル例

📄 **`config-system-prompts-example.json`** - 実装例
- 実際の JSON 設定ファイル
- systemPromptSettings セクション
- 2 つのプロンプント内容を含む

---

## 🎯 用途別 ドキュメント選択ガイド

### "プロンプントとは？動作を理解したい"
1. `SYSTEM-PROMPTS-QUICK-REFERENCE.md` (5 分)
2. `system-prompts-guide.md` (20 分)

### "画面でプロンプットを変更したい"
1. `SYSTEM-PROMPTS-QUICK-REFERENCE.md` (5 分)
2. `system-prompts-settings.md` - 「画面からのプロンプット変更」セクション (10 分)

### "設定ファイルでプロンプットを設定したい"
1. `config-system-prompts-example.json` - 例を参照 (3 分)
2. `system-prompts-settings.md` - 「設定ファイルからの読み込み」セクション (10 分)

### "起動時に自動的にプロンプットを適用したい"
1. `system-prompts-settings.md` - 「起動時の自動適用」セクション (5 分)
2. MainActivity.cs:282 - `ApplySystemPromptSettings()` メソッドを確認 (5 分)

### "新しいプロンプットタイプを追加したい"
1. `SYSTEM-PROMPTS-QUICK-REFERENCE.md` - 「新しいプロンプントタイプの追加」(10 分)
2. `system-prompts-implementation.md` - 「将来の拡張」セクション (10 分)

### "トラブルを解決したい"
1. `SYSTEM-PROMPTS-QUICK-REFERENCE.md` - 「トラブルシューティング」(5 分)
2. `system-prompts-guide.md` - 「トラブルシューティング」セクション (15 分)
3. ログを確認 - `[SettingsService]` と `[MainActivity]` のログを確認

### "実装詳細を知りたい"
1. `system-prompts-implementation.md` (30 分)

### "設定機能を実装・拡張したい"
1. `system-prompts-settings.md` (20 分)
2. SystemPromptsActivity.cs を確認 (15 分)

---

## 📋 機能一覧と対応ドキュメント

### 基本機能（システムプロンプント定義）

| 機能 | 説明 | ドキュメント |
|------|------|-----------|
| **Conversation プロンプット** | 通常の会話（ラバーダックデバッグ型） | `system-prompts-guide.md` |
| **SemanticValidation プロンプット** | 音声認識の意味検証と修正 | `system-prompts-guide.md` |
| **プロンプット切り替え** | OpenAIService のプロンプット設定変更 | `system-prompts-implementation.md` |
| **デフォルトプロンプット** | SystemPrompts.cs に定義された初期値 | `SYSTEM-PROMPTS-SUMMARY.md` |

### 設定機能

| 機能 | 説明 | ドキュメント |
|------|------|-----------|
| **画面からの変更** | SystemPromptsActivity でプロンプント編集 | `system-prompts-settings.md` |
| **ファイルからの読み込み** | JSON 設定ファイルから自動適用 | `system-prompts-settings.md` |
| **起動時の自動適用** | アプリ起動時にプロンプット設定反映 | `system-prompts-settings.md` |
| **SharedPreferences 保存** | 変更されたプロンプット設定を永続保存 | `system-prompts-settings.md` |
| **デフォルトリセット** | 「デフォルトにリセット」ボタン | `system-prompts-settings.md` |

---

## 🏗️ システム構成図

```
┌─────────────────────────────────────────────────────────────┐
│                         Robin アプリ                         │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ MainActivity                                          │  │
│  │  └─ ApplySystemPromptSettings()                      │  │
│  │     ├─ SettingsService.LoadSystemPromptSettings()   │  │
│  │     └─ OpenAIService.SetSystemPrompt()              │  │
│  └──────────────────────────────────────────────────────┘  │
│                           ↑ ↓                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ SystemPromptsActivity (画面)                         │  │
│  │  ├─ 「システムプロンプト設定」メニューから起動      │  │
│  │  └─ SaveSystemPromptSettings()                       │  │
│  └──────────────────────────────────────────────────────┘  │
│                           ↓                                │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ SettingsService                                      │  │
│  │  ├─ SaveSystemPromptSettings()  (SharedPreferences) │  │
│  │  ├─ LoadSystemPromptSettings()  (SharedPreferences) │  │
│  │  ├─ ApplyConfiguration()        (JSON ファイル)     │  │
│  │  └─ ClearSystemPromptSettings()                     │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↓                          ↓                     │
│  ┌─────────────────┐      ┌──────────────────────┐        │
│  │ SharedPreferences│      │ JSON 設定ファイル     │        │
│  │ (ローカル保存)  │      │ (外部インポート)     │        │
│  │                 │      │                      │        │
│  │ conv_prompt  ←─┼──────┤ systemPromptSettings │        │
│  │ sem_prompt   ←─┼──────┤  ├─ convPrompt      │        │
│  │ use_custom   ←─┼──────┤  ├─ semPrompt       │        │
│  │               │      │  └─ useCustom       │        │
│  └─────────────────┘      └──────────────────────┘        │
│           ↓                                                │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ OpenAIService                                        │  │
│  │  ├─ SetSystemPrompt(PromptType)                     │  │
│  │  ├─ SetSystemPrompt(customString)                   │  │
│  │  ├─ GetSystemPrompt()                              │  │
│  │  └─ BuildRequest() → API に system メッセージ送信  │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↓                                                │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ OpenAI API                                           │  │
│  │ (system メッセージ含むリクエスト送信)               │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📖 推奨される読む順序

### 初めての人向け

```
1. SYSTEM-PROMPTS-QUICK-REFERENCE.md        (5分)
   ↓ 基本的な使い方を理解
2. system-prompts-guide.md                   (20分)
   ↓ 2 つのプロンプントを詳しく理解
3. system-prompts-settings.md (導入部)       (10分)
   ↓ 設定機能の概要を理解
4. 実装して試す
```

### 開発者向け

```
1. SYSTEM-PROMPTS-SUMMARY.md                 (10分)
   ↓ 実装内容の全体像を把握
2. system-prompts-implementation.md          (30分)
   ↓ 技術詳細を理解
3. SYSTEM-PROMPTS-SETTINGS-SUMMARY.md        (10分)
   ↓ 設定機能の全体像を把握
4. system-prompts-settings.md (詳細部分)     (20分)
   ↓ 設定機能の実装詳細を理解
5. コードを確認
```

### 設定機能を使いたい人向け

```
1. SYSTEM-PROMPTS-SETTINGS-SUMMARY.md        (10分)
   ↓ 概要を理解
2. system-prompts-settings.md                (15分)
   ↓ 詳細を理解
3. config-system-prompts-example.json        (参考)
   ↓ JSON 形式を確認
4. 画面またはファイルで設定
```

---

## 🔧 トラブルシューティング

### よくある質問

**Q: プロンプット変更後、アプリに反映されない**
- A: アプリを再起動してください。起動時に `ApplySystemPromptSettings()` が呼ばれます。

**Q: JSON 設定ファイルから読み込まれない**
- A: `system-prompts-settings.md` の「トラブルシューティング」を確認

**Q: デフォルトプロンプットに戻したい**
- A: 画面の「デフォルトにリセット」ボタンをタップ

**Q: プロンプット設定をクリアしたい**
- A: `SettingsService.ClearSystemPromptSettings()` を呼び出し

---

## 📊 ファイル統計

| ドキュメント | ファイル数 | 合計サイズ | 主なトピック |
|------------|---------|---------|-----------|
| クイックリファレンス | 1 | 6.9 KB | コード例・使用方法 |
| 完全ガイド | 1 | 11 KB | 概念・ベストプラクティス |
| 技術仕様 | 1 | 11 KB | API・実装詳細 |
| 設定機能 | 1 | 10 KB | 画面・ファイル設定 |
| サマリー | 2 | 8.1 + 10 KB | 概要・完了報告 |
| 設定例 | 1 | 4 KB | JSON サンプル |
| **合計** | **7** | **60 KB** | 包括的なドキュメント |

---

## ✅ チェックリスト

読むべきドキュメント：

- [ ] `SYSTEM-PROMPTS-QUICK-REFERENCE.md` - 基本的な使い方
- [ ] `system-prompts-guide.md` - 詳細説明
- [ ] `system-prompts-settings.md` - 設定機能
- [ ] `config-system-prompts-example.json` - JSON 設定例

実装の確認：

- [ ] メニューに「システムプロンプト設定」が表示される
- [ ] SystemPromptsActivity でプロンプットを編集できる
- [ ] 設定を保存・変更できる
- [ ] 設定ファイルから自動読み込みできる
- [ ] アプリ起動時に設定が反映される

---

## 🎯 まとめ

Robin のシステムプロンプト機能は：

✅ **2 つのプリセット** - Conversation と SemanticValidation
✅ **動的カスタマイズ** - 画面からいつでも変更可能
✅ **ファイル対応** - JSON 設定ファイルから自動適用
✅ **自動反映** - 起動時に自動的に設定が適用される
✅ **包括的なドキュメント** - 7 つのドキュメント（合計 60 KB）

すべてのドキュメントを活用して、Robin のプロンプット機能を最大限に活用してください！

---

**最終更新**: 2025-10-28
**ビルド状態**: ✅ 成功（46 警告、0 エラー）
**ドキュメント完成度**: 100%
