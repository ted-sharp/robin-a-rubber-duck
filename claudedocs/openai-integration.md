# OpenAI API 統合ガイド

## 概要

Robin アプリケーションは、**OpenAI API** と **LM Studio** の両方に対応しています。
これにより、クラウドベースのAIモデル（GPT-4o など）とローカルモデルの両方を使用できます。

## 対応するLLMプロバイダー

| プロバイダー | 説明 | APIキー | インターネット | コスト |
|------------|------|--------|-------------|--------|
| **OpenAI** | クラウドベースのOpenAI API | 必須 | 必須 | 従量課金 |
| **LM Studio** | ローカルで実行される言語モデル | 不要 | 不要 | 無料 |

## OpenAI API の設定

### ステップ 1: OpenAI APIキーを取得

1. [OpenAI公式ページ](https://platform.openai.com/) にアクセス
2. ログイン（または登録）
3. **API Keys** ページへ移動
4. **+ Create new secret key** をクリック
5. 生成されたAPIキーをコピー（形式: `sk-...`）

### ステップ 2: 設定ファイルを作成

以下の内容で `config.json` を作成：

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

**設定項目の説明:**
- `provider`: `"openai"` に設定
- `endpoint`: OpenAI APIエンドポイント（変更不要）
- `modelName`: 使用するモデル（例: `gpt-4o`, `gpt-4-turbo`, `gpt-3.5-turbo`）
- `apiKey`: 取得したAPIキー
- `isEnabled`: 機能を有効にするか

### ステップ 3: アプリで設定をインポート

1. Robin アプリを起動
2. メニューから「設定」を選択
3. 「設定ファイルを選択」をタップ
4. 作成した `config.json` を選択
5. 設定が自動的に適用されます

## LM Studio の設定

### ステップ 1: LM Studio をセットアップ

1. [LM Studio 公式サイト](https://lmstudio.ai/) からダウンロード
2. インストールして起動
3. モデルをダウンロード
4. **Local Server** で API サーバーを起動

### ステップ 2: 設定ファイルを作成

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

**設定項目の説明:**
- `provider`: `"lm-studio"` に設定
- `endpoint`: LM Studio のエンドポイント（ローカルネットワークのIPアドレス:ポート）
- `modelName`: ダウンロードしたモデル名
- `apiKey`: `null` で問題ありません（LM Studioはキー不要）
- `isEnabled`: 機能を有効にするか

### ステップ 3: ネットワーク設定

LM Studio をPCで実行し、AndroidデバイスからアクセスするためにはWi-Fi設定が重要です：

1. PC と Android デバイスを同じWi-Fiネットワークに接続
2. PC の IPアドレスを確認：
   - Windows: `ipconfig` コマンドで「IPv4 アドレス」を確認
   - Mac/Linux: `ifconfig` または `ip addr` コマンド
3. エンドポイントをそのIPアドレスで設定

### ステップ 4: アプリで設定をインポート

上記 OpenAI API の「ステップ 3」と同様

## 利用可能なモデル

### OpenAI の推奨モデル

| モデル | 説明 | 推奨用途 |
|-------|------|--------|
| `gpt-4o` | 最新・最高性能 | 一般的な用途、高精度が必要な場合 |
| `gpt-4-turbo` | 高性能・高速 | バランス型、コスト重視 |
| `gpt-3.5-turbo` | 高速・低コスト | 単純なタスク、コスト最小化 |

### LM Studio の推奨モデル

- `openai/gpt-oss-20b`: 高速、メモリ効率的
- `meta-llama/Llama-2-7b`: 軽量、単純なタスク向け
- その他ローカルモデル多数

## プロバイダーの切り替え

### OpenAI から LM Studio へ

1. 設定ファイルで `provider` を `"lm-studio"` に変更
2. `endpoint` と `modelName` を LM Studio に合わせて更新
3. `apiKey` を `null` に設定
4. ファイルをインポート

### LM Studio から OpenAI へ

1. 設定ファイルで `provider` を `"openai"` に変更
2. `endpoint` を `"https://api.openai.com/v1/"` に設定
3. `apiKey` に OpenAI APIキーを設定
4. `modelName` を OpenAI モデルに変更
5. ファイルをインポート

## トラブルシューティング

### OpenAI API エラー

**エラー: "OpenAI APIキーが設定されていません"**
- `apiKey` フィールドに正しいAPIキーを設定してください
- APIキーの形式が `sk-` で始まっているか確認

**エラー: "API Error (401)"**
- APIキーが無効または期限切れの可能性
- OpenAI公式ページで新しいAPIキーを生成してください

**エラー: "API Error (429)"**
- レート制限に達しています
- 少し待ってから再度試してください

### LM Studio エラー

**エラー: "接続に失敗しました"**
- LM Studio が起動しているか確認
- エンドポイントのIPアドレスとポート番号を確認
- PC と Android が同じWi-Fiネットワークにいるか確認

**エラー: "モデルが見つかりません"**
- LM Studio でモデルが正しくダウンロードされているか確認
- `modelName` が LM Studio 内の実際のモデル名と一致しているか確認

## 実装の詳細

### アーキテクチャ

```
Configuration (JSON)
    ↓
SettingsService (読み込み)
    ↓
LLMProviderSettings (解析)
    ↓
MainActivity (初期化)
    ↓
OpenAIService (選択されたプロバイダーで初期化)
    ↓
API リクエスト
```

### クラス構成

- **LLMProviderSettings**: LLMプロバイダー設定の定義
- **Configuration**: JSON設定ファイル全体の構造
- **LLMSettings**: Configuration 内の LLM 設定
- **OpenAIService**: OpenAI API と LM Studio 両方に対応
- **SettingsService**: 設定の読み込み・保存

### 対応する環境

- **最小 Android バージョン**: API 24 (Android 7.0)
- **推奨 Android バージョン**: API 29以上

## セキュリティベストプラクティス

### OpenAI APIキーの管理

⚠️ **非常に重要**

1. **APIキーを共有しない**
   - メール、メッセージ、ソースコードで共有しない
   - Git/GitHub にコミットしない

2. **定期的なキー更新**
   - 定期的に新しいキーを生成
   - 不要になったキーは削除

3. **キー漏洩時の対応**
   - OpenAI ダッシュボードで直ちに無効化
   - 新しいキーを生成

4. **環境変数の使用（推奨）**
   - 本番環境では設定ファイルにキーを含めない
   - 環境変数から読み込む仕組みを構築

### 設定ファイルの取り扱い

- 設定ファイルはアプリケーション内に保存しないこと
- 必要時のみ、信頼できるソースからインポート
- 他の人と共有する場合はAPIキーを削除

## コスト管理

### OpenAI API の料金

- GPT-4o: 入力トークン当たり $0.005/1K トークン、出力当たり $0.015/1K トークン
- GPT-4 Turbo: 更に安い料金体系
- GPT-3.5 Turbo: さらに低コスト

詳細は [OpenAI 料金ページ](https://openai.com/pricing/) を参照

### コスト削減のコツ

1. **モデル選択**: 不要な高度なモデルを避ける
2. **キャッシング**: 同じクエリに対してはローカルキャッシュを活用
3. **プロンプト最適化**: 不要な情報を削除
4. **LM Studio 検討**: 無料のローカル実行を優先

## 関連ファイル

- `Models/LMStudioSettings.cs`: LLM設定クラス
- `Models/Configuration.cs`: JSON設定構造
- `Services/OpenAIService.cs`: API実装
- `Services/SettingsService.cs`: 設定管理
- `claudedocs/config-openai-example.json`: OpenAI設定例
- `claudedocs/config-lmstudio-example.json`: LM Studio設定例
