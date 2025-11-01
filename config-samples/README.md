# Robin - 外部API設定サンプルファイル

このディレクトリには、Robinアプリで使用できる外部API設定のサンプルファイルが含まれています。

## 使い方

1. サンプルファイルをコピーして、実際の設定値に書き換えます
2. Androidデバイスの任意の場所（例: `/sdcard/Download/`, `/storage/emulated/0/Documents/` など）にファイルをコピーします
3. Robinアプリを起動し、サイドドロワーから設定メニューを開きます
4. 「設定ファイルを選択」をタップしてファイルピッカーで任意の場所から設定ファイルを選択します

## LLM (会話AI) 設定

### OpenAI
**ファイル**: `llm-openai-sample.json`

```json
{
  "provider": "openai",
  "endpoint": "https://api.openai.com/v1",
  "apiKey": "sk-proj-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "modelName": "gpt-4o-mini",
  "isEnabled": true
}
```

**設定項目**:
- `provider`: `"openai"` (固定)
- `endpoint`: `"https://api.openai.com/v1"` (通常は変更不要)
- `apiKey`: OpenAI APIキー (https://platform.openai.com/api-keys で取得)
- `modelName`: 使用するモデル名
  - `gpt-4o-mini` (コスパ重視)
  - `gpt-4o` (高性能)
  - `gpt-3.5-turbo` (低コスト)
- `isEnabled`: `true` で有効化

### Azure OpenAI
**ファイル**: `llm-azure-openai-sample.json`

```json
{
  "provider": "azure-openai",
  "endpoint": "https://YOUR-RESOURCE-NAME.openai.azure.com",
  "apiKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "modelName": "gpt-4o-mini",
  "isEnabled": true
}
```

**設定項目**:
- `provider`: `"azure-openai"` (固定)
- `endpoint`: Azure OpenAIリソースのエンドポイントURL
- `apiKey`: Azure OpenAI APIキー
- `modelName`: デプロイメント名
- `isEnabled`: `true` で有効化

### LM Studio (ローカルLLM)
**ファイル**: `llm-lm-studio-sample.json`

```json
{
  "provider": "lm-studio",
  "endpoint": "http://192.168.0.7:1234",
  "apiKey": null,
  "modelName": "qwen2.5-14b-instruct",
  "isEnabled": true
}
```

**設定項目**:
- `provider`: `"lm-studio"` (固定)
- `endpoint`: LM StudioサーバーのURL (通常 `http://[PC-IP]:1234`)
- `apiKey`: `null` (LM Studioでは不要)
- `modelName`: LM Studioで読み込んでいるモデル名
- `isEnabled`: `true` で有効化

**LM Studio セットアップ**:
1. PCでLM Studioを起動
2. モデルをダウンロード・読み込み
3. 「Server」タブでAPIサーバーを起動
4. スマホとPCを同じWi-Fiに接続
5. PCのIPアドレスを確認して `endpoint` に設定

### Claude (Anthropic)
**ファイル**: `llm-claude-sample.json`

```json
{
  "provider": "claude",
  "endpoint": "https://api.anthropic.com/v1",
  "apiKey": "sk-ant-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "modelName": "claude-3-5-sonnet-20241022",
  "isEnabled": true
}
```

**設定項目**:
- `provider`: `"claude"` (固定)
- `endpoint`: `"https://api.anthropic.com/v1"` (通常は変更不要)
- `apiKey`: Anthropic APIキー (https://console.anthropic.com/ で取得)
- `modelName`: 使用するモデル名
  - `claude-3-5-sonnet-20241022` (最新・高性能)
  - `claude-3-5-haiku-20241022` (高速・低コスト)
- `isEnabled`: `true` で有効化

## ASR (音声認識) 設定

### Azure Speech-to-Text
**ファイル**: `asr-azure-stt-sample.json`

```json
{
  "subscriptionKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "region": "japaneast",
  "language": "ja-JP",
  "endpointId": null,
  "enableDictation": false,
  "enableProfanityFilter": false,
  "speechRecognitionLanguageAutoDetectionMode": 0
}
```

**設定項目**:
- `subscriptionKey`: Azure Cognitive Services APIキー
- `region`: Azureリージョン
  - `japaneast` (東日本)
  - `japanwest` (西日本)
  - `eastus` (米国東部)
- `language`: 認識言語
  - `ja-JP` (日本語)
  - `en-US` (英語)
  - `zh-CN` (中国語)
- `endpointId`: カスタムモデルのエンドポイントID (通常は `null`)
- `enableDictation`: ディクテーションモード有効化
- `enableProfanityFilter`: 不適切な言葉のフィルタリング
- `speechRecognitionLanguageAutoDetectionMode`: 言語自動検出
  - `0`: 無効
  - `1`: 開始時のみ
  - `2`: 継続的

### Faster Whisper (LAN内サーバー)
**ファイル**: `asr-faster-whisper-sample.json`

```json
{
  "provider": "faster-whisper",
  "endpoint": "http://192.168.0.10:8000",
  "apiKey": null,
  "language": "ja",
  "modelName": null,
  "isEnabled": true
}
```

**設定項目**:
- `provider`: `"faster-whisper"` (固定)
- `endpoint`: Faster WhisperサーバーのURL
- `apiKey`: `null` (通常は不要)
- `language`: 認識言語
  - `ja` (日本語)
  - `en` (英語)
  - `zh` (中国語)
- `modelName`: `null` (サーバー側で設定)
- `isEnabled`: `true` で有効化

**Faster Whisper サーバーセットアップ**:
```bash
# Python環境にインストール
pip install faster-whisper flask

# サーバー起動スクリプト例
python faster_whisper_server.py --host 0.0.0.0 --port 8000 --model large-v3
```

## 設定ファイルの配置

### Androidデバイスへのコピー方法

**方法1: USBケーブル経由**
1. AndroidデバイスをPCにUSB接続
2. ファイル転送モードを選択
3. 任意の場所（例: `/sdcard/Download/`, `/storage/emulated/0/Documents/` など）にJSONファイルをコピー

**方法2: ADB経由**
```bash
# Downloadフォルダの場合
adb push llm-openai-sample.json /sdcard/Download/

# または任意のパスへ
adb push llm-openai-sample.json /storage/emulated/0/Documents/
```

**方法3: クラウドストレージ経由**
1. Google DriveやDropboxにアップロード
2. Androidデバイスでダウンロード
3. 任意のフォルダーに保存できます（アプリ起動時にファイルピッカーで選択）

## トラブルシューティング

### 設定ファイルが見つからない
- ファイルが適切なフォルダに配置されているか確認（`/sdcard/Download/`, `/storage/emulated/0/Documents/` など）
- ファイル拡張子が `.json` になっているか確認
- ファイル名に全角文字が含まれていないか確認
- ファイルピッカーで表示されない場合、ストレージアクセス権限を確認

### APIキーエラー
- APIキーが正しくコピーされているか確認
- APIキーの前後にスペースがないか確認
- APIキーの有効期限が切れていないか確認

### 接続エラー (LM Studio / Faster Whisper)
- スマホとサーバーが同じネットワークに接続されているか確認
- ファイアウォールでポートがブロックされていないか確認
- サーバーが起動しているか確認
- エンドポイントURLが正しいか確認 (http:// から始まる)

## セキュリティ上の注意

- APIキーは絶対に他人と共有しないでください
- 設定ファイルをGitHubなどの公開リポジトリにアップロードしないでください
- 使用後は不要な設定ファイルを削除することを推奨します
- LM StudioやFaster Whisperを外部ネットワークに公開しないでください

## 参考リンク

- **OpenAI API**: https://platform.openai.com/docs/api-reference
- **Azure OpenAI**: https://learn.microsoft.com/azure/ai-services/openai/
- **Anthropic Claude**: https://docs.anthropic.com/
- **LM Studio**: https://lmstudio.ai/
- **Azure Speech**: https://learn.microsoft.com/azure/ai-services/speech-service/
- **Faster Whisper**: https://github.com/SYSTRAN/faster-whisper
