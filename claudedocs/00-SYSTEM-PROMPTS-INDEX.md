# システムプロンプト実装 - ドキュメントインデックス

## 📋 概要

Robin に 2 種類のシステムプロンプトを実装しました：

1. **Conversation プロンプト** - 通常の会話（ラバーダックデバッグ型）
2. **SemanticValidation プロンプト** - 音声認識の意味検証と誤認識修正

---

## 📚 ドキュメントガイド

### 👤 ユーザー向け

#### 1. **SYSTEM-PROMPTS-QUICK-REFERENCE.md** ⭐ 最初にこれ！

**読むべき人**: 機能を使いたい開発者

**内容**:
- 2 つのプロンプトの概要
- よく使う実装パターン
- コード例
- 新しいプロンプトタイプの追加方法
- よくある質問と回答

**所要時間**: 5～10 分

---

#### 2. **system-prompts-guide.md** - 完全ガイド

**読むべき人**: 詳しく理解したい開発者

**内容**:
- システムプロンプトの概念説明
- Conversation プロンプトの詳細
- SemanticValidation プロンプトの詳細
- ライフサイクル
- パフォーマンス考慮事項
- カスタマイズ方法
- デバッグ方法
- ベストプラクティス
- トラブルシューティング

**所要時間**: 20～30 分

---

### 👨‍💻 開発者向け

#### 3. **system-prompts-implementation.md** - 技術仕様

**読むべき人**: 実装を理解したい開発者

**内容**:
- ファイル構成
- 修正内容の詳細
- メソッド実装
- プロンプント内容の詳細
- API リクエストフロー
- トークン使用量
- デバッグポイント
- セキュリティ考慮事項
- 拡張方法

**所要時間**: 30～45 分

---

#### 4. **SYSTEM-PROMPTS-SUMMARY.md** - 実装完了サマリー

**読むべき人**: 実装内容を素早く把握したい

**内容**:
- 実装内容の概要
- ファイル構成
- ドキュメント一覧
- 実装の特徴
- テスト推奨項目
- ビルド状態
- 次のステップ

**所要時間**: 10～15 分

---

## 🗂️ ファイル構成

### コード

```
Models/
└── SystemPrompts.cs
    ├── ConversationSystemPrompt (定数)
    ├── SemanticValidationSystemPrompt (定数)
    ├── PromptType (enum)
    └── GetSystemPrompt() (メソッド)

Services/
├── OpenAIService.cs (修正)
│   ├── _systemPrompt フィールド追加
│   ├── SetSystemPrompt() メソッド追加
│   ├── GetSystemPrompt() メソッド追加
│   └── BuildRequest() メソッド修正
│
└── SemanticValidationService.cs (修正)
    ├── ValidateAsync() プロンプト切り替え機能追加
    └── BuildValidationPrompt() 簡潔化
```

### ドキュメント

```
claudedocs/
├── 00-SYSTEM-PROMPTS-INDEX.md (このファイル)
├── SYSTEM-PROMPTS-QUICK-REFERENCE.md (クイックリファレンス)
├── SYSTEM-PROMPTS-SUMMARY.md (完了サマリー)
├── system-prompts-guide.md (完全ガイド)
└── system-prompts-implementation.md (技術仕様)
```

---

## 🚀 クイックスタート

### 1. すぐに使いたい場合

**→ SYSTEM-PROMPTS-QUICK-REFERENCE.md を読む** (5 分)

```csharp
// Conversation プロンプト（デフォルト）
var response = await _openAIService.SendMessageAsync(messages);

// SemanticValidation プロンプムに一時的に切り替え
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.SemanticValidation);
// LLM 処理...
_openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);  // 復帰
```

### 2. 詳しく理解したい場合

**→ system-prompts-guide.md を読む** (20 分)

- プロンプントの概念から実装まで
- ベストプラクティス
- トラブルシューティング

### 3. 実装の詳細を知りたい場合

**→ system-prompts-implementation.md を読む** (30 分)

- API 仕様
- トークン計算
- セキュリティ
- 拡張方法

### 4. 実装結果を知りたい場合

**→ SYSTEM-PROMPTS-SUMMARY.md を読む** (10 分)

- 何が実装されたか
- ビルド状態
- 次のステップ

---

## 🎯 用途別ガイド

### "通常の会話機能を実装したい"

1. SYSTEM-PROMPTS-QUICK-REFERENCE.md の「ケース 1」を確認
2. デフォルトの Conversation プロンプトが自動で使用される

### "新しいプロンプトタイプを追加したい"

1. SYSTEM-PROMPTS-QUICK-REFERENCE.md の「新しいプロンプトタイプの追加」を確認
2. system-prompts-implementation.md の「将来の拡張」を参照

### "音声認識の誤認識修正を実装したい"

1. SYSTEM-PROMPTS-SUMMARY.md の「実装内容」を確認
2. SemanticValidationService の動作を理解
3. system-prompts-guide.md の「SemanticValidation プロンプント」を詳読

### "プロンプントをカスタマイズしたい"

1. system-prompts-guide.md の「カスタマイズ方法」を確認
2. system-prompts-implementation.md の「セキュリティ考慮事項」を確認

### "トラブルシューティングしたい"

1. SYSTEM-PROMPTS-QUICK-REFERENCE.md の「トラブルシューティング」を確認
2. system-prompts-guide.md の「トラブルシューティング」を詳読
3. system-prompts-implementation.md の「デバッグ時の確認ポイント」を参照

---

## 📊 実装の特徴一覧

| 特徴 | 説明 | ドキュメント |
|------|------|-----------|
| **柔軟性** | プロンプのタイプまたはカスタム文字列で設定 | Quick Ref / Guide |
| **安全性** | エラー時も元のプロンプトに復帰 | Implementation |
| **拡張性** | 新しいプロンプントタイプを簡単に追加可能 | Quick Ref / Implementation |
| **互換性** | 既存 API は変わらない | Summary / Implementation |
| **効率性** | トークン使用量を最適化 | Guide / Implementation |

---

## ✅ チェックリスト

### 実装完了項目

- ✅ 2 種類のシステムプロンプトを定義
- ✅ Conversation プロンプント（ラバーダック型）
- ✅ SemanticValidation プロンプント（意味検証）
- ✅ SystemPrompts.cs クラスを作成
- ✅ OpenAIService にプロンプト管理機能を追加
- ✅ SemanticValidationService にプロンプト自動切り替えを実装
- ✅ 包括的なドキュメントを作成
- ✅ ビルド成功（0 エラー）

### テスト推奨項目

- [ ] Conversation プロンプットでの通常会話テスト
- [ ] SemanticValidation プロンプットでの意味検証テスト
- [ ] 同音異義語の修正確認（例：「タス苦」→「タスク」）
- [ ] 意味不明な入力の検出確認
- [ ] プロンプット切り替え後の復帰確認
- [ ] ログ出力の確認
- [ ] トークン使用量の確認

---

## 📞 サポート

### 質問がある場合

1. **Quick Reference** を確認（FAQ セクション）
2. **Guide** の「トラブルシューティング」を確認
3. **Implementation** のデバッグセクションを確認

### バグを見つけた場合

1. **Implementation** のデバッグポイントを確認
2. ログ出力を確認
3. API リクエスト内容を確認

### 機能を追加したい場合

1. **Implementation** の「将来の拡張」を確認
2. **Quick Reference** の「新しいプロンプントタイプの追加」を確認

---

## 🔗 関連ドキュメント

- `processing-state-display.md` - 処理状態表示機能
- `message-layout-optimization.md` - メッセージレイアウト最適化
- `semantic-validation-buffer-implementation.md` - 意味検証バッファ
- `architecture-design.md` - システムアーキテクチャ

---

## 📝 ドキュメント詳細

| ドキュメント | 用途 | 読者 | 所要時間 | リンク |
|-------------|------|------|---------|-------|
| Quick Reference | 実装パターン集 | 開発者 | 5-10 分 | ⭐ 最初 |
| Summary | 完成サマリー | 全員 | 10-15 分 | 2 番目 |
| Guide | 完全ガイド | 詳しく知りたい人 | 20-30 分 | 3 番目 |
| Implementation | 技術仕様 | 実装者 | 30-45 分 | 詳細 |

---

## 🎓 学習ロードマップ

```
初めて
  ↓
Quick Reference (5 分)
  ↓
実装して試す
  ↓
問題が出た？
  ├→ Guide (20 分)
  └→ Implementation (30 分)
  ↓
カスタマイズしたい
  ↓
Implementation (30 分)
```

---

## 📊 統計

- **作成したファイル**: 1 個（SystemPrompts.cs）
- **修正したファイル**: 2 個（OpenAIService.cs, SemanticValidationService.cs）
- **ドキュメント**: 5 個（このインデックス含む）
- **合計行数**: 約 1,500 行（ドキュメント含む）
- **ビルド状態**: ✅ 成功（0 エラー）

---

## 🏁 まとめ

このシステムプロンプト実装により、Robin は以下を実現します：

✅ **親しみやすい会話** - Conversation プロンプットで自然な対話
✅ **正確な音声認識** - SemanticValidation プロンプットで誤認識を修正
✅ **柔軟な拡張** - 新しいプロンプットタイプを簡単に追加可能
✅ **安全な管理** - プロンプット切り替え後の確実な復帰処理

ドキュメントに従って、Robin を最大限に活用してください！

---

**最終更新**: 2025-10-28
**ビルド状態**: ✅ 成功
**ドキュメント完成度**: 100%
