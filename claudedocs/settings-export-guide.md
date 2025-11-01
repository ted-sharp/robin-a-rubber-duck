# 設定ファイルエクスポート機能ガイド

## 概要

Robin アプリケーションは、現在の設定をJSONファイルとしてエクスポートする機能をサポートしています。

これにより、複数のデバイス間での設定共有や、バックアップの作成が容易になります。

## 主な用途

- **複数デバイスでの設定共有** - エクスポートした設定ファイルを別のデバイスでインポート
- **設定のバックアップ** - デバイスリセット前に設定を保存
- **設定の検証** - JSON形式で設定を確認・編集
- **チームでの設定管理** - 同じ環境で複数人が作業する場合の設定統一

## エクスポート方法

### ステップ1: SettingsActivity を開く

1. Robin アプリを起動
2. メニューから「設定」を選択
3. SettingsActivity（LM Studio設定画面）を開く

### ステップ2: 「設定をエクスポート」ボタンをタップ

```
┌─────────────────────────────────┐
│  LM Studio設定                   │
├─────────────────────────────────┤
│  エンドポイント: http://...      │
│  モデル名: gpt-3.5-turbo         │
│  [ ] 有効                         │
├─────────────────────────────────┤
│  [保存] [戻る]                   │
│  [設定ファイルを選択]            │
│  [設定をエクスポート] ← タップ   │
└─────────────────────────────────┘
```

### ステップ3: エクスポート完了

```
✅ エクスポート完了
/sdcard/Download/robin_config_20250101_120000.json
```

ファイルはダウンロードフォルダに自動保存されます。

## エクスポートされるファイル形式

### デフォルト形式（APIキー除外）

```json
{
  "llmSettings": {
    "provider": "openai",
    "endpoint": "https://api.openai.com/v1",
    "apiKey": null,
    "modelName": "gpt-4o",
    "isEnabled": true
  },
  "sttSettings": {
    "provider": "sherpa-onnx",
    "endpoint": null,
    "apiKey": null,
    "language": "ja",
    "modelName": "sherpa-onnx-sense-voice",
    "isEnabled": true
  },
  "systemPromptSettings": {
    "conversationPrompt": "You are a helpful assistant...",
    "semanticValidationPrompt": null,
    "useCustomPrompts": true
  },
  "voiceSettings": {
    "engine": "sherpa-onnx",
    "language": "ja"
  },
  "otherSettings": {
    "verboseLogging": false,
    "theme": "light"
  }
}
```

## セキュリティに関する注意

### ✅ デフォルト動作（APIキー除外）

```
【推奨】 APIキーは含めずにエクスポート
- GitHub等での共有が安全
- 他人と設定内容を共有できる
- APIキーの漏洩リスクがない
```

### ⚠️ APIキー含有（非推奨）

```
【非推奨】 APIキーを含めてエクスポート
- 本人のみが使用するバックアップとして
- 共有すると APIキーが漏洩するリスク
- 厳重なアクセス管理が必要
```

**重要:** APIキーを含むエクスポートファイルは、以下の点に注意してください。

- ❌ GitHub等の公開リポジトリにアップロード厳禁
- ❌ Cloud Drive への無暗号化アップロード厳禁
- ❌ メールで送信厳禁
- ✅ ローカルストレージのみで保管
- ✅ 不要になったら削除

## エクスポートファイルの使用方法

### 別のデバイスで設定をインポート

1. エクスポートしたJSONファイルをAndroidデバイスに転送
2. Robin アプリを起動
3. 「設定」から「設定ファイルを選択」
4. エクスポートしたJSONファイルを選択
5. 自動的に設定がインポートされます

### JSONファイルを手動編集

エクスポートしたJSONファイルは、テキストエディタで編集可能です：

```json
{
  "llmSettings": {
    "provider": "openai",
    "endpoint": "https://api.openai.com/v1",
    "apiKey": "sk-proj-xxxxxxxxxxxxxxxx",  // 手動で追加可能
    "modelName": "gpt-4o",  // モデルを変更可能
    "isEnabled": true
  }
}
```

編集後は、そのファイルをアプリでインポートできます。

## ファイル名の形式

```
robin_config_yyyyMMdd_HHmmss.json

例）robin_config_20250101_120000.json
    - 2025年1月1日
    - 12時00分00秒
    - にエクスポートしたファイル
```

## トラブルシューティング

### エクスポートに失敗する

**問題:** 「ダウンロードフォルダにアクセスできません」というエラー

**原因:**
- ストレージアクセス権限がない
- ダウンロードフォルダが存在しない

**解決方法:**
```
1. アプリの設定 → 権限 で「ストレージ」をON
2. デバイスを再起動
3. 再度エクスポートを試す
```

### JSONファイルが見つからない

**問題:** エクスポート成功メッセージが表示されても、ファイルが見つからない

**確認方法:**
```
1. ファイルマネージャーを起動
2. ダウンロードフォルダを開く
3. 「robin_config_」で始まるファイルを検索
4. ソート順序を「最終更新日」にして確認
```

### JSONファイルが編集できない

**問題:** JSONファイルをテキストエディタで開いても文字化けする

**原因:** ファイルの文字コードがUTF-8でない

**解決方法:**
```
- 専用のテキストエディタを使用
  例）VS Code, Sublime Text, JSONエディタ
- ファイルをUTF-8で保存
```

## バージョン間の互換性

### 同じバージョン間
```
✅ 完全互換
robin_config_v1.0.0.json → robin v1.0.0 でインポート可能
```

### 異なるバージョン間
```
⚠️ 部分互換（新しい設定フィールドは無視される）
robin_config_v1.0.0.json → robin v1.1.0 でインポート可能（ただし一部設定が失われる可能性）
```

**推奨:** 同じバージョン番号のアプリ間でのみ設定をやり取りしてください。

## 実装の詳細

### 関連ファイル

- `Services/SettingsService.cs` - エクスポート実装
- `SettingsActivity.cs` - UI実装
- `Models/Configuration.cs` - 設定スキーマ

### エクスポート処理フロー

```
ExportConfiguration() メソッド
    ↓
SharedPreferences から現在の設定を読み込み
    ↓
Configuration オブジェクトに変換
    ↓
JSON形式にシリアライズ
    ↓
ファイルに書き込み
    ↓
✅ エクスポート完了
```

### APIキー除外処理

```csharp
if (!includeApiKeys)
{
    config.LLMSettings.ApiKey = null;   // APIキーを削除
    config.STTSettings.ApiKey = null;   // APIキーを削除
}
```

## 今後の拡張予定

- [ ] UIダイアログでAPIキー含有/除外を選択
- [ ] クラウドへの自動バックアップ
- [ ] バックアップファイルの暗号化
- [ ] 差分バックアップ機能
- [ ] 設定のバージョン管理

## ベストプラクティス

### 定期的なバックアップ

```
推奨：1ヶ月に1回、またはAPIキー変更時にエクスポート
```

### セキュアなバックアップ方法

```
手順：
1. APIキー除外でエクスポート（JSONファイル）
2. APIキーは別ファイルで安全に管理
3. JSONファイル本体は安全なクラウドに保存
```

### 設定ファイルの整理

```
推奨フォルダ構成：
Downloads/
├── robin_config_20250101_120000.json  # 本番環境
├── robin_config_20241201_150000.json  # テスト環境
└── robin_config_old/                  # アーカイブ
    ├── robin_config_20241101_100000.json
    └── robin_config_20240901_090000.json
```

## 参考リンク

- [設定ファイルのインポートガイド](./configuration-import-guide.md)
- [EncryptedSharedPreferences セキュリティガイド](./encrypted-settings-guide.md)
