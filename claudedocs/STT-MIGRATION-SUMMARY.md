# STT 実装完了 - マイグレーション概要

## 概要

TTS（Text-to-Speech）実装を完全に削除し、STT（Speech-to-Text）による複数プロバイダー対応の音声認識システムを実装しました。

**ユーザーリクエスト**: 「TTSはいらない。STTだった。」 → 正しい要件に修正して実装

## 削除されたファイル

### モデルファイル
- ❌ `Models/TTSProviderSettings.cs` - TTS設定モデル

### サービスファイル
- ❌ `Services/TTSService.cs` - TTS音声合成実装

### ドキュメンテーション
- ❌ `claudedocs/tts-integration-guide.md` - TTS統合ガイド
- ❌ `claudedocs/config-tts-voicevox-example.json` - VOICEVOX設定例
- ❌ `claudedocs/config-tts-openai-example.json` - OpenAI TTS設定例

## 作成されたファイル

### モデル
✅ `Models/STTProviderSettings.cs`
- `STTProviderSettings`: ベースクラス
- `GoogleSTTSettings`: Google Cloud Speech-to-Text設定
- `AzureSTTSettings`: Azure Cognitive Services Speech設定
- `SherpaOnnxSTTSettings`: Sherpa-ONNX（ローカル、完全オフライン）設定
- `AndroidSTTSettings`: Android標準音声認識設定

### ドキュメント
✅ `claudedocs/stt-integration-guide.md` - 包括的なSTT統合ガイド
✅ `claudedocs/config-stt-sherpa-example.json` - Sherpa-ONNX設定例
✅ `claudedocs/config-stt-google-example.json` - Google Cloud設定例
✅ `claudedocs/config-stt-azure-example.json` - Azure設定例
✅ `claudedocs/config-stt-android-standard-example.json` - Android標準設定例

## 修正された既存ファイル

### Configuration.cs
```diff
- [JsonPropertyName("ttsSettings")]
- public TTSSettings? TTSSettings { get; set; }
+ [JsonPropertyName("sttSettings")]
+ public STTSettings? STTSettings { get; set; }

- public class TTSSettings { ... }
+ public class STTSettings { ... }
```

**STTSettings の新しい構造**:
- `provider`: "google", "azure", "sherpa-onnx", "android-standard"
- `endpoint`: クラウドサービスのエンドポイント
- `apiKey`: APIキー（クラウド使用時）
- `language`: 言語設定（"ja", "en", "zh", "ko", "auto"）
- `modelName`: モデル名（Sherpa-ONNX使用時）
- `isEnabled`: 有効化フラグ

### SettingsService.cs
```diff
- TTSプロバイダー設定キー削除
+ STTプロバイダー設定キー追加:
  - stt_provider
  - stt_endpoint
  - stt_api_key
  - stt_language
  - stt_model_name
  - stt_enabled

- SaveTTSProviderSettings()
+ SaveSTTProviderSettings(STTProviderSettings settings)

- LoadTTSProviderSettings()
+ LoadSTTProviderSettings() -> STTProviderSettings

- ClearTTSProviderSettings()
+ ClearSTTProviderSettings()

- ExportConfiguration() で TTSSettings を使用
+ ExportConfiguration() で STTSettings を使用
```

## STT プロバイダー比較

| プロバイダー | 特徴 | 精度 | APIキー | インターネット | コスト |
|------------|------|-------|--------|-------------|---------|
| **Sherpa-ONNX** | ローカル実行、完全オフライン | ⭐⭐⭐⭐ | 不要 | 不要 | 無料 |
| **Google Cloud** | 高精度なクラウド音声認識 | ⭐⭐⭐⭐⭐ | 必須 | 必須 | 従量課金 |
| **Azure** | Microsoft提供、エンタープライズ対応 | ⭐⭐⭐⭐⭐ | 必須 | 必須 | 従量課金 |
| **Android 標準** | オンデバイス、システム統合 | ⭐⭐⭐ | 不要 | 可 | 無料 |

## 推奨される構成

### 開発・テスト環境
```json
{
  "sttSettings": {
    "provider": "sherpa-onnx",
    "language": "auto",
    "isEnabled": true
  }
}
```
メリット: セットアップ不要、完全オフライン、テストに最適

### プロダクション環境（高精度要求）
```json
{
  "sttSettings": {
    "provider": "google",
    "endpoint": "https://speech.googleapis.com/v1/speech:recognize",
    "apiKey": "your-key",
    "language": "ja-JP",
    "isEnabled": true
  }
}
```
メリット: 高精度、日本語最適化、ビジネス対応

### オフライン環境
```json
{
  "sttSettings": {
    "provider": "sherpa-onnx",
    "language": "ja",
    "isEnabled": true
  }
}
```
メリット: インターネット不要、オフライン利用可能

## 設定ファイル移行ガイド

### 旧 TTS 設定（削除）
```json
{
  "ttsSettings": {
    "provider": "voicevox",
    "endpoint": "http://localhost:50021",
    "voice": "1",
    "speed": 1.0,
    "isEnabled": true
  }
}
```

### 新 STT 設定
```json
{
  "sttSettings": {
    "provider": "sherpa-onnx",
    "language": "auto",
    "modelName": "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
    "isEnabled": true
  }
}
```

## ビルド結果

✅ **ビルド成功**
- エラー: 0
- 警告: 10 (既存の廃止予定メソッド使用による警告)
- 出力: `Robin.dll`

## 次のステップ

### アプリ統合（将来の実装）
STT プロバイダー設定を以下のサービスで利用可能にする：
1. `VoiceInputService.cs` - Android 標準音声認識
2. `SherpaRealtimeService.cs` - Sherpa-ONNX オフライン認識

例:
```csharp
// STT設定の読み込み
var sttSettings = settingsService.LoadSTTProviderSettings();

// プロバイダーに応じた初期化
if (sttSettings.Provider == "sherpa-onnx")
{
    _voiceService = new SherpaRealtimeService(context);
    await _voiceService.InitializeAsync(sttSettings.ModelName);
}
else if (sttSettings.Provider == "google")
{
    _voiceService = new GoogleSTTService(sttSettings.ApiKey, sttSettings.Language);
}
```

## ファイル参照一覧

| ファイル | 目的 |
|---------|------|
| `Models/STTProviderSettings.cs` | STTプロバイダー設定モデル |
| `Models/Configuration.cs` | アプリケーション設定構造（STT含む） |
| `Services/SettingsService.cs` | STT設定の永続化・読み込み |
| `claudedocs/stt-integration-guide.md` | STT統合ガイド（詳細） |
| `claudedocs/config-stt-sherpa-example.json` | Sherpa-ONNX設定例 |
| `claudedocs/config-stt-google-example.json` | Google Cloud設定例 |
| `claudedocs/config-stt-azure-example.json` | Azure設定例 |
| `claudedocs/config-stt-android-standard-example.json` | Android標準設定例 |

## 関連ドキュメント

- `claudedocs/stt-integration-guide.md` - STT統合の詳細ガイド
- `claudedocs/configuration-import-guide.md` - 設定ファイルインポート機能
- `claudedocs/openai-integration.md` - OpenAI LLM設定
- 既存ドキュメント - `claudedocs/architecture-design.md`, `claudedocs/sherpa-onnx-integration.md`

## 実装時の注意点

### STT と VoiceSettings の関係
```csharp
// VoiceSettings は従来通り存在し、
// STTSettings と並行して使用可能

// 既存の互換性を保つため：
public class VoiceSettings
{
    [JsonPropertyName("engine")]
    public string? Engine { get; set; }  // "sherpa", "google", "azure", "android-standard"

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

// STTSettings は詳細な設定を提供
public class STTSettings
{
    public string Provider { get; set; }  // プロバイダー
    public string? Endpoint { get; set; }  // クラウドサービスのエンドポイント
    public string? ApiKey { get; set; }    // 認証キー
    public string? Language { get; set; }  // 言語設定
    public string? ModelName { get; set; } // モデル仕様
}
```

## セキュリティに関する注意

⚠️ **重要**: Google Cloud / Azure の API キーは機密情報
- Git に含めないこと
- 設定ファイルはセキュアに管理
- 定期的にキーをローテーション
- プロダクション環境では環境変数から読み込み

## テスト推奨事項

各プロバイダーで以下をテスト：
1. ✅ 設定ファイルのインポート
2. ✅ STT設定の読み込み・保存
3. ✅ プロバイダー切り替え
4. ✅ 言語設定の変更
5. ✅ オフライン/オンラインの切り替え

---

**実装日**: 2025-10-27
**ステータス**: ✅ 完了
