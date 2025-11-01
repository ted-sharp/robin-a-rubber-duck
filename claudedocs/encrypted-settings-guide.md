# EncryptedSharedPreferences を使用したセキュアな設定管理

## 概要

Robin アプリケーションはAPIキーなどの機密情報をセキュアに保存するために、**EncryptedSharedPreferences** をサポートしています。

このガイドでは、APIキーやその他の機密データを暗号化して保存する方法について説明します。

## セキュリティモデル

### 通常の SharedPreferences
```
❌ 平文で保存
❌ root権限でデバイスにアクセスされるとデータが漏洩
❌ apk を unzip すると SharedPreferences のファイルにアクセス可能
```

### EncryptedSharedPreferences（新機能）
```
✅ AES-256 GCM で暗号化
✅ Master Key は Android Keystore で保護
✅ 物理的な root アクセスでもデータが保護される
✅ Google 推奨の暗号化方式
```

## 使用方法

### 1. APIキーを暗号化保存

**従来の方法（平文保存）:**
```csharp
// 通常の SharedPreferences（推奨されない）
var settings = new LLMProviderSettings("openai", "https://api.openai.com/v1", "gpt-4o", apiKey, true);
_settingsService.SaveLLMProviderSettings(settings);  // 平文で保存
```

**セキュアな方法（暗号化保存）:**
```csharp
// EncryptedSharedPreferences を使用
var settings = new LLMProviderSettings("openai", "https://api.openai.com/v1", "gpt-4o", apiKey, true);
_settingsService.SaveLLMProviderSettings(settings, useSecureStorage: true);  // 暗号化して保存
```

### 2. 暗号化された設定を読み込み

```csharp
// EncryptedSettingsService を直接使用
var encryptedSettings = new EncryptedSettingsService(context);
var settings = encryptedSettings.LoadLLMProviderSettingsSecurely();

// または SettingsService 経由
var settingsService = new SettingsService(context);
var settings = settingsService.LoadLLMProviderSettings();  // 自動的に適切なストレージから読み込む
```

### 3. 設定内容の例

**LLM プロバイダー設定（暗号化保存）:**
```csharp
var apiKey = "sk-proj-xxxxxxxxxxxxxxxxxxxxx";  // 機密情報
var settings = new LLMProviderSettings(
    provider: "openai",
    endpoint: "https://api.openai.com/v1",
    modelName: "gpt-4o",
    apiKey: apiKey,  // 暗号化される
    isEnabled: true
);

// apiKey が暗号化されて保存される
_encryptedSettings.SaveLLMProviderSettingsSecurely(settings);
```

## 暗号化の仕組み

### マスターキー管理

```
┌─────────────────────────────────┐
│  Android Keystore (Hardware)    │
│  (TrustZone / Secure Enclave)   │
└────────────┬────────────────────┘
             │
             ▼
   ┌─────────────────────┐
   │  Master Key (AES)   │
   │  robin_master_key   │
   └─────────────────────┘
             │
             ▼
  ┌───────────────────────────────┐
  │  EncryptedSharedPreferences    │
  │  - LLM API Key (AES-256-GCM)  │
  │  - STT API Key (AES-256-GCM)  │
  │  - Endpoints                  │
  └───────────────────────────────┘
```

### 暗号化フロー

```
APIキー入力
    │
    ▼
AES-256 GCM で暗号化
    │
    ▼
Android Keystore から Master Key を取得
    │
    ▼
EncryptedSharedPreferences に保存
```

## セキュリティベストプラクティス

### ✅ 推奨される事項

1. **APIキーは常に暗号化保存**
```csharp
_settingsService.SaveLLMProviderSettings(settings, useSecureStorage: true);
```

2. **設定ファイルのインポート後は暗号化保存**
```csharp
if (config?.LLMSettings != null)
{
    var settings = new LLMProviderSettings(
        config.LLMSettings.Provider,
        config.LLMSettings.Endpoint,
        config.LLMSettings.ModelName,
        config.LLMSettings.ApiKey,  // 入力されたAPIキー
        config.LLMSettings.IsEnabled
    );
    // 暗号化して保存
    _settingsService.SaveLLMProviderSettings(settings, useSecureStorage: true);
}
```

3. **使用後は不要な機密データをクリア**
```csharp
_encryptedSettings.ClearEncryptedSettings();
```

### ❌ 避けるべき事項

1. ❌ APIキーを平文でログに出力
```csharp
// これはしない
Log.Info("Settings", $"API Key: {apiKey}");  // 危険！
```

2. ❌ APIキーを設定ファイルに平文で保存してGitHubにコミット
```json
// リポジトリにコミットしない！
{
  "apiKey": "sk-proj-xxxxxxxxxxxxxxxxxxxxx"
}
```

3. ❌ 機密情報を通常の SharedPreferences に保存
```csharp
// これは使わない
editor.PutString("api_key", apiKey);  // 暗号化されない
```

## トラブルシューティング

### EncryptedSharedPreferences 初期化エラー

**問題:** ロギングで「EncryptedSettingsService の初期化に失敗」というメッセージが表示される

**原因:**
- Android Keystore が利用不可（古いデバイスなど）
- 権限不足

**解決方法:**
```csharp
// フォールバック: 通常の SharedPreferences を使用
_settingsService.SaveLLMProviderSettings(settings, useSecureStorage: false);
```

### デバイスリセット後の設定喪失

**問題:** デバイスをリセットすると暗号化された設定が失われる

**これは正常な動作です。** Android Keystore のキーはデバイスリセットで削除されるため、特定のデバイスに紐付けられた暗号化データは復旧できません。

**対策:** 設定ファイルエクスポート機能を使用してバックアップを作成してください。

## 実装の詳細

### 関連ファイル

- `Services/EncryptedSettingsService.cs` - 暗号化設定管理
- `Services/SettingsService.cs` - 統合設定管理（オプション）
- `Models/LLMProviderSettings.cs` - LLM設定モデル
- `Models/STTProviderSettings.cs` - STT設定モデル

### 暗号化スキーム

| 項目 | 値 |
|------|-----|
| キー暗号化方式 | AES-256-SIV |
| 値暗号化方式 | AES-256-GCM |
| マスターキー生成 | Android Keystore (AES-256) |
| 完全性チェック | GCM による AEAD |

### クラス構成

```
SettingsService
├── SharedPreferences（通常の設定）
│   ├── LLM設定（平文）
│   ├── STT設定（平文）
│   └── その他設定
│
└── EncryptedSettingsService
    ├── Master Key (from Android Keystore)
    ├── EncryptedSharedPreferences
    │   ├── LLM API Key（AES-256-GCM暗号化）
    │   ├── STT API Key（AES-256-GCM暗号化）
    │   └── その他の機密データ
```

## 今後の拡張予定

- [ ] Biometric 認証によるアクセス制御
- [ ] キーローテーション機能
- [ ] バックアップの暗号化
- [ ] リモートキー管理対応

## 参考リンク

- [Android Security: SharedPreferences](https://developer.android.com/training/articles/keystore)
- [AndroidX Security: EncryptedSharedPreferences](https://android-developers.googleblog.com/2019/02/encrypt-sensitive-data.html)
- [Google BestPractices: Credential Management](https://developer.android.com/training/sign-in/credential-manager)
