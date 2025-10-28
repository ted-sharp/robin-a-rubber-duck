# 設定ファイルのインポート機能ガイド

## 概要

Robin アプリケーションは、JSON形式の設定ファイルをインポートして、複数の設定を一度に適用できます。これにより、異なるデバイス間での設定共有や、初期セットアップの効率化が実現できます。

複数のLLMプロバイダーに対応しており、OpenAI APIまたはLM Studioを自由に選択できます。

## 対応する設定

### 1. LLM（言語モデル）設定 - 複数プロバイダー対応
- **プロバイダー**: 使用するLLMプロバイダー（"openai" または "lm-studio"）
- **エンドポイント**: APIサーバーのURL（LM Studio使用時、またはカスタムエンドポイント）
- **APIキー**: OpenAI API使用時に必須
- **モデル名**: 使用するモデルの名前
- **有効化**: LLM機能の有効/無効

### 2. 音声認識設定
- **エンジン**: 使用する音声認識エンジン（"sherpa" または "android-standard"）
- **言語**: 認識対象言語（"ja", "en", "zh", "ko"など）
- **モデル名**: 使用する音声認識モデル

### 3. その他の設定
- **詳細ログ**: ログ出力の詳細度
- **テーマ**: UIテーマの選択

## 設定ファイルの形式

設定ファイルは JSON 形式で、以下の構造を持ちます：

```json
{
  "llmSettings": {
    "provider": "lm-studio",
    "endpoint": "http://192.168.0.7:1234",
    "modelName": "openai/gpt-oss-20b",
    "apiKey": null,
    "isEnabled": true
  },
  "voiceSettings": {
    "engine": "sherpa",
    "language": "ja",
    "modelName": "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09"
  },
  "otherSettings": {
    "verboseLogging": false,
    "theme": "light"
  }
}
```

## 使用方法

### ステップ1: 設定ファイルを準備
1. 上記のJSON形式で設定ファイルを作成
2. ファイル名は `config.json` または任意の `.json` ファイル名で保存
3. ファイルをAndroidデバイスに転送（例：ダウンロードフォルダなど）

### ステップ2: アプリで設定をインポート
1. Robin アプリを起動
2. メニューから「設定」を選択
3. SettingsActivity が開いたら「設定ファイルを選択」ボタンをタップ
4. ファイルピッカーダイアログが表示される
5. 準備した JSON設定ファイルを選択
6. アプリが自動的に設定を読み込み、適用します
7. 「設定を保存」ボタンをタップして設定を確定

## 設定ファイルの例

### LM Studio 設定
```json
{
  "llmSettings": {
    "provider": "lm-studio",
    "endpoint": "http://192.168.0.7:1234",
    "modelName": "openai/gpt-oss-20b",
    "apiKey": null,
    "isEnabled": true
  }
}
```

### OpenAI API 設定
```json
{
  "llmSettings": {
    "provider": "openai",
    "endpoint": "https://api.openai.com/v1/",
    "modelName": "gpt-4o",
    "apiKey": "sk-your-api-key-here",
    "isEnabled": true
  }
}
```

### LLM 無効の場合
```json
{
  "llmSettings": {
    "provider": "lm-studio",
    "endpoint": "http://localhost:1234",
    "modelName": "gpt-3.5-turbo",
    "apiKey": null,
    "isEnabled": false
  }
}
```

### 多言語対応設定（OpenAI + 英語音声認識）
```json
{
  "llmSettings": {
    "provider": "openai",
    "endpoint": "https://api.openai.com/v1/",
    "modelName": "gpt-4o",
    "apiKey": "sk-your-api-key-here",
    "isEnabled": true
  },
  "voiceSettings": {
    "engine": "sherpa",
    "language": "en",
    "modelName": "sherpa-onnx-whisper-tiny"
  }
}
```

## 対応する設定値

### engine（音声認識エンジン）
- `"sherpa"` - Sherpa-ONNX（オフライン認識）
- `"android-standard"` - Android標準（オンライン認識）

### language（言語コード）
- `"ja"` - 日本語
- `"en"` - 英語
- `"zh"` - 中国語（簡体）
- `"ko"` - 韓国語

### theme（UIテーマ）
- `"light"` - ライトテーマ
- `"dark"` - ダークテーマ

## トラブルシューティング

### ファイルが見つからないエラー
- ファイルの場所を確認してください
- ファイル名が `.json` で終わっていることを確認
- ファイルが読み取り可能な状態であることを確認

### JSON解析エラー
- JSONの構文が正しいか確認（括弧、カンマなど）
- JSONオンラインバリデーター（例: jsonlint.com）で検証
- ダブルクォートが正しく使用されているか確認

### 設定が適用されない
- 各値のデータ型を確認（文字列は `"xxx"`、真偽値は `true/false`、null値は `null`）
- `isEnabled` が正しい真偽値か確認
- `provider` が `"openai"` または `"lm-studio"` か確認
- アプリを再起動して反映を確認

### OpenAI API 設定エラー
- APIキーが正しく設定されているか確認（`apiKey` が null ではない）
- APIキーの形式が `sk-` で始まっているか確認
- OpenAI公式ページでAPIキーを再度確認
- インターネット接続を確認

### LM Studio 接続エラー
- LM Studio がPC上で起動しているか確認
- エンドポイントのIPアドレスが正しいか確認（例: `192.168.0.7` はローカルIPアドレス）
- ポート番号が正しいか確認（デフォルト: `1234`）
- PCとAndroidデバイスが同じWi-Fiネットワークに接続しているか確認

## 実装の詳細

### アーキテクチャ
1. **SettingsService**: 設定ファイルの読み込み・保存を処理
2. **Configuration モデル**: JSON設定の構造を定義
3. **SettingsActivity**: UI上でファイル選択とインポート処理を実行

### ファイルパス処理
- **content:// URI**: MediaStore経由のファイルアクセス（推奨）
- **file:// URI**: ファイルシステム直接アクセス
- **キャッシュフォールバック**: ContentResolver経由でキャッシュに一時保存

### エラーハンドリング
- JSONのパース失敗時: エラーログ出力＆ユーザーへのトースト通知
- ファイルアクセス失敗時: キャッシュへの自動コピー試行
- 無効な値: デフォルト値でフォールバック

## セキュリティの考慮事項

⚠️ **重要**: OpenAI APIキーの取り扱いに注意してください
- APIキーは機密情報です。絶対に他の人と共有しないでください
- 設定ファイルを他人と共有する場合は、APIキーを削除してください
- GitやGitHub等のバージョン管理システムにAPIキーを含める設定ファイルをコミットしないでください
- 万が一APIキーが漏洩した場合は、OpenAI公式ページから直ちに無効化してください

その他のセキュリティ対策：
- 設定ファイルは平文で保存されるため、デバイスへの物理的なアクセスを制限してください
- 本番環境では、設定ファイルの暗号化の実装を検討してください
- ユーザーが信頼できるソースからのファイルのみをインポートしてください

## 今後の拡張予定

- [ ] 設定ファイルのエクスポート機能
- [ ] 設定ファイルの暗号化
- [ ] クラウド同期機能
- [ ] 複数の設定プロファイル管理
- [ ] 差分更新対応

## 技術仕様

### 関連ファイル
- `Models/Configuration.cs`: 設定ファイルの構造定義
- `Models/LMStudioSettings.cs`: LM Studio設定
- `Services/SettingsService.cs`: 設定の読み込み・保存
- `SettingsActivity.cs`: UI実装
- `Resources/layout/activity_settings.xml`: UIレイアウト

### 権限要件
- `android.permission.READ_EXTERNAL_STORAGE`: ファイル読み込み
- `android.permission.READ_MEDIA_JSON`: JSON形式ファイルへのアクセス

### 対応API
- Android API 24以上

## 参考リンク

- サンプル設定ファイル: `config-sample.json`
- OpenAI API ドキュメント: https://platform.openai.com/docs
- Sherpa-ONNX: https://github.com/k2-fsa/sherpa-onnx
- LM Studio: https://lmstudio.ai/
