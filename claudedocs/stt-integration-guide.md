# Speech-to-Text (STT) 統合ガイド

## 概要

Robin アプリケーションは、複数のSTT（音声認識）プロバイダーに対応しており、音声をテキストに変換できます。

対応するプロバイダー：
- **Google Cloud Speech-to-Text**: クラウドベース、高精度、多言語対応
- **Azure Cognitive Services Speech**: Microsoft提供、高精度、エンタープライズ対応
- **Sherpa-ONNX**: 無料、ローカル実行、完全オフライン、日本語特化
- **Android 標準**: オンデバイス、システム統合、デバイス依存

## 対応するプロバイダー

| プロバイダー | 説明 | 精度 | APIキー | インターネット | コスト |
|------------|------|-------|--------|-------------|---------|
| **Google Cloud** | 高精度な音声認識 | ⭐⭐⭐⭐⭐ | 必須 | 必須 | 従量課金 |
| **Azure** | Microsoft提供 | ⭐⭐⭐⭐⭐ | 必須 | 必須 | 従量課金 |
| **Sherpa-ONNX** | ローカル実行、完全オフライン、日本語特化 | ⭐⭐⭐⭐ | 不要 | 不要 | 無料 |
| **Android 標準** | オンデバイス、システム統合 | ⭐⭐⭐ | 不要 | 可 | 無料 |

## 各プロバイダーの設定

### 1. Sherpa-ONNX（推奨：ローカル、完全オフライン、無料）

#### セットアップ

アプリに組み込まれているため、追加セットアップ不要です。

#### 設定ファイル例

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
    "language": "ja"
  },
  "sttSettings": {
    "provider": "sherpa-onnx",
    "endpoint": null,
    "apiKey": null,
    "language": "auto",
    "modelName": "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
    "isEnabled": true
  },
  "otherSettings": {
    "verboseLogging": false,
    "theme": "light"
  }
}
```

**設定項目:**
- `provider`: `"sherpa-onnx"` に固定
- `endpoint`: null（ローカル実行のため不要）
- `apiKey`: null（認証不要）
- `language`: 言語設定（`"auto"`, `"ja"`, `"en"`, `"zh"`, `"ko"`）
- `modelName`: 使用するモデル名
- `isEnabled`: 有効化するか

#### 対応言語

- `"auto"`: 自動言語検出
- `"ja"`: 日本語
- `"en"`: 英語
- `"zh"`: 中国語（標準中国語）
- `"ko"`: 韓国語
- `"yue"`: 広東語（カントン語）

#### 利用可能なモデル

| モデル | 説明 |
|--------|------|
| `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09` | SenseVoice int8量子化版（推奨、軽量） |
| `sherpa-onnx-sense-voice` | SenseVoice フル精度版（高精度、容量大） |

### 2. Google Cloud Speech-to-Text

#### セットアップ

1. [Google Cloud Console](https://console.cloud.google.com/) でプロジェクトを作成
2. Speech-to-Text API を有効化
3. APIキーを取得

#### 設定ファイル例

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
    "engine": "google",
    "language": "ja-JP"
  },
  "sttSettings": {
    "provider": "google",
    "endpoint": "https://speech.googleapis.com/v1/speech:recognize",
    "apiKey": "your-google-api-key",
    "language": "ja-JP",
    "modelName": null,
    "isEnabled": true
  },
  "otherSettings": {
    "verboseLogging": false,
    "theme": "light"
  }
}
```

**設定項目:**
- `provider`: `"google"` に固定
- `endpoint`: Google Speech-to-Text エンドポイント
- `apiKey`: Google APIキー（必須）
- `language`: 言語コード（`"ja-JP"`, `"en-US"` など）
- `modelName`: null（Google では不要）
- `isEnabled`: 有効化するか

#### 対応言語

| コード | 言語 |
|-------|------|
| `ja-JP` | 日本語 |
| `en-US` | 英語（米国） |
| `zh-CN` | 中国語（簡体字） |
| `ko-KR` | 韓国語 |

### 3. Azure Cognitive Services Speech

#### セットアップ

1. [Azure ポータル](https://portal.azure.com/) でアカウントを作成
2. Speech service を作成
3. API キーとリージョンを取得

#### 設定ファイル例

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
    "engine": "azure",
    "language": "ja-JP"
  },
  "sttSettings": {
    "provider": "azure",
    "endpoint": "https://japaneast.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1",
    "apiKey": "your-azure-api-key",
    "language": "ja-JP",
    "modelName": null,
    "isEnabled": true
  },
  "otherSettings": {
    "verboseLogging": false,
    "theme": "light"
  }
}
```

**設定項目:**
- `provider`: `"azure"` に固定
- `endpoint`: Azure Speech エンドポイント（リージョンに応じて変更）
- `apiKey`: Azure APIキー（必須）
- `language`: 言語コード
- `modelName`: null（Azure では不要）
- `isEnabled`: 有効化するか

#### リージョンごとのエンドポイント

| リージョン | エンドポイント |
|-----------|-------------|
| 日本東 | `https://japaneast.stt.speech.microsoft.com` |
| 日本西 | `https://japanwest.stt.speech.microsoft.com` |
| 米国東 | `https://eastus.stt.speech.microsoft.com` |

### 4. Android 標準音声認識

#### セットアップ

1. Android デバイスに Google 音声認識がインストールされていることを確認
2. 設定で音声認識言語を選択

#### 設定ファイル例

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
    "engine": "android-standard",
    "language": "ja-JP"
  },
  "sttSettings": {
    "provider": "android-standard",
    "endpoint": null,
    "apiKey": null,
    "language": "ja-JP",
    "modelName": null,
    "isEnabled": true
  },
  "otherSettings": {
    "verboseLogging": false,
    "theme": "light"
  }
}
```

**設定項目:**
- `provider`: `"android-standard"` に固定
- `endpoint`: null（デバイスローカル）
- `apiKey`: null（認証不要）
- `language`: 言語コード
- `modelName`: null（不要）
- `isEnabled`: 有効化するか

## アプリでの使用方法

### ステップ1: 設定ファイルを準備

上記の例を参考に、使用するプロバイダーに合わせて設定ファイル（`config.json`）を作成します。

### ステップ2: アプリで設定をインポート

1. Robin アプリを起動
2. メニューから「設定」を選択
3. 「設定ファイルを選択」をタップ
4. 作成した `config.json` を選択
5. 設定が適用されます

### ステップ3: 使用開始

アプリを起動して、マイク ボタンをタップすると、設定されたSTTプロバイダーで音声認識が開始されます。

## トラブルシューティング

### Sherpa-ONNX

**エラー: "モデル初期化失敗"**
- モデルファイルがアプリに正しく含まれているか確認
- `modelName` が正しく設定されているか確認

**エラー: "音声認識に失敗しました"**
- マイク権限が許可されているか確認
- 音声データが正しく録音されているか確認

### Google Cloud

**エラー: "Invalid API Key"**
- APIキーが正しい形式か確認
- プロジェクトで Speech-to-Text API が有効化されているか確認

**エラー: "403 Forbidden"**
- API キーに適切なアクセス権限があるか確認
- API クォータが超過していないか確認

### Azure

**エラー: "Unauthorized"**
- APIキーが正しいか確認
- リージョンが正しいか確認

**エラー: "Connection timeout"**
- ネットワーク接続を確認
- エンドポイント URL が正しいか確認

### Android 標準

**エラー: "音声認識リスナーが使用できません"**
- デバイスに Google 音声認識がインストールされているか確認
- インターネット接続を確認（Google 音声認識はオンライン機能）

## セキュリティと料金管理

### APIキーの取り扱い

⚠️ **重要**: APIキーは機密情報です
- 他人と共有しないでください
- Git等に含めないでください
- 定期的に新しいキーを生成してください

### 料金削減

1. **Sherpa-ONNX を優先**: ローカル実行で完全無料
2. **API利用を最小化**: 必要な時のみ音声認識を実行
3. **言語検出**: `"auto"` 設定で言語指定の精度向上

## 実装の詳細

### VoiceInputService クラス（Android 標準）

```csharp
public class VoiceInputService
{
    public event EventHandler<string>? RecognitionResult;

    public void StartListening()
    {
        // 音声認識開始
    }

    public void StopListening()
    {
        // 音声認識停止
    }
}
```

### SherpaRealtimeService クラス（Sherpa-ONNX）

```csharp
public class SherpaRealtimeService
{
    public event EventHandler<string>? FinalResult;

    public async Task InitializeAsync(string modelPath)
    {
        // モデル初期化
    }

    public void StartListening()
    {
        // 音声認識開始
    }
}
```

### STTProviderSettings クラス

```csharp
public class STTProviderSettings
{
    public string Provider { get; set; } // "google", "azure", "sherpa-onnx", "android-standard"
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Language { get; set; }
    public string? ModelName { get; set; }
    public bool IsEnabled { get; set; }
}
```

### SettingsService での STT 設定管理

```csharp
// STT設定を読み込む
var sttSettings = settingsService.LoadSTTProviderSettings();

// STT設定を保存
settingsService.SaveSTTProviderSettings(newSettings);

// STT設定をクリア
settingsService.ClearSTTProviderSettings();
```

## 関連ファイル

- `Models/STTProviderSettings.cs`: STT設定クラス
- `Services/VoiceInputService.cs`: Android 標準音声認識実装
- `Services/SherpaRealtimeService.cs`: Sherpa-ONNX 実装
- `claudedocs/config-stt-sherpa-example.json`: Sherpa-ONNX 設定例
- `claudedocs/config-stt-google-example.json`: Google Cloud 設定例
- `claudedocs/config-stt-azure-example.json`: Azure 設定例

## 参考リンク

- [Sherpa-ONNX GitHub](https://github.com/k2-fsa/sherpa-onnx)
- [Google Cloud Speech-to-Text](https://cloud.google.com/speech-to-text)
- [Azure Cognitive Services Speech](https://azure.microsoft.com/ja-jp/services/cognitive-services/speech-to-text/)
- [Android SpeechRecognizer](https://developer.android.com/reference/android/speech/SpeechRecognizer)
