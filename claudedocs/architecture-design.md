# Robin - 音声対話Androidアプリ アーキテクチャ設計

## 概要

Discord風UIを持つ音声対話Androidアプリケーション。音声入力をテキスト変換し、OpenAI APIへ送信してAIレスポンスを表示する。

## 技術スタック

- **フレームワーク**: .NET 10.0 Android (net10.0-android)
- **最小Androidバージョン**: API 24 (Android 7.0)
- **言語**: C# 12 with nullable reference types
- **UI**: ネイティブAndroid Views (DrawerLayout, RecyclerView, etc.)

## アーキテクチャ構成

### UI構造

```
DrawerLayout
├─ NavigationView (左側メニュー)
│   ├─ ヘッダー (ユーザー情報)
│   └─ メニューアイテム
│       ├─ チャット
│       ├─ 設定
│       └─ バージョン情報
│
└─ FrameLayout (メインコンテンツ)
    └─ ChatFragment
        ├─ RecyclerView (チャット履歴)
        │   ├─ UserMessageView
        │   └─ AIMessageView
        ├─ VoiceInputButton (FloatingActionButton)
        └─ StatusText (音声認識状態表示)
```

### コンポーネント設計

#### 1. UI層

**MainActivity.cs**
```
役割: アプリのエントリーポイント、DrawerLayout管理
責務:
- DrawerLayoutの初期化
- ナビゲーション処理
- Fragment管理
- 権限リクエスト (RECORD_AUDIO)
```

**ChatFragment.cs**
```
役割: メインチャット画面
責務:
- RecyclerViewの管理
- マイクボタンのイベント処理
- 音声入力状態の表示
- メッセージ送受信のUI更新
```

**MessageAdapter.cs**
```
役割: チャット履歴のRecyclerViewアダプター
責務:
- メッセージリストの表示
- ユーザー/AI メッセージの区別表示
- ViewHolder管理
```

#### 2. サービス層

**VoiceInputService.cs**
```
役割: 音声認識機能の提供
API: Android SpeechRecognizer
責務:
- 音声認識の開始/停止
- 音声→テキスト変換
- 認識結果のコールバック
- エラーハンドリング

主要メソッド:
- StartListening(): void
- StopListening(): void
- OnResult(string recognizedText): event
- OnError(SpeechRecognizerError error): event
```

**OpenAIService.cs**
```
役割: OpenAI API通信
API: OpenAI Chat Completion API
責務:
- HTTPリクエスト構築
- API認証 (Bearer Token)
- JSON送受信
- エラーハンドリング

主要メソッド:
- SendMessageAsync(string message): Task<string>
- BuildRequest(List<Message> messages): HttpRequestMessage
- ParseResponse(HttpResponseMessage response): Task<string>
```

**ConversationService.cs**
```
役割: 会話履歴の管理
責務:
- メッセージ履歴の保持
- ユーザー/AIメッセージの追加
- 履歴のクリア
- データ永続化 (オプション)

主要メソッド:
- AddUserMessage(string text): void
- AddAIMessage(string text): void
- GetMessages(): List<Message>
- ClearHistory(): void
```

#### 3. データモデル

**Message.cs**
```csharp
public class Message
{
    public string Id { get; set; }          // UUID
    public MessageRole Role { get; set; }   // User or Assistant
    public string Content { get; set; }     // メッセージ内容
    public DateTime Timestamp { get; set; } // 送信時刻
}

public enum MessageRole
{
    User,
    Assistant
}
```

**OpenAIRequest.cs / OpenAIResponse.cs**
```csharp
// OpenAI API用のDTO
public class OpenAIRequest
{
    public string Model { get; set; }       // "gpt-4" etc.
    public List<ApiMessage> Messages { get; set; }
    public double Temperature { get; set; }
}

public class ApiMessage
{
    public string Role { get; set; }        // "user" or "assistant"
    public string Content { get; set; }
}

public class OpenAIResponse
{
    public string Id { get; set; }
    public List<Choice> Choices { get; set; }
}

public class Choice
{
    public ApiMessage Message { get; set; }
}
```

### 処理フロー詳細

#### シーケンス: 音声入力→AI応答

```
1. ユーザーがマイクボタンタップ
   ↓
2. ChatFragment.OnMicButtonClick()
   ↓
3. VoiceInputService.StartListening()
   → Android SpeechRecognizer起動
   → UI: 録音中インジケーター表示
   ↓
4. ユーザーが発話
   ↓
5. VoiceInputService.OnResult(recognizedText)
   → ChatFragment: テキスト受信
   → UI: ユーザーメッセージ表示
   ↓
6. ConversationService.AddUserMessage(recognizedText)
   ↓
7. OpenAIService.SendMessageAsync(recognizedText)
   → HTTPリクエスト構築
   → OpenAI APIへPOST
   → UI: "AI考え中..." 表示
   ↓
8. OpenAIService: レスポンス受信
   → JSON パース
   ↓
9. ConversationService.AddAIMessage(response)
   ↓
10. ChatFragment: AI応答をUI表示
    → RecyclerView更新
```

#### エラーハンドリング

**音声認識エラー**
- ERROR_NO_MATCH: "音声が認識できませんでした"
- ERROR_NETWORK: "ネットワークエラーが発生しました"
- ERROR_AUDIO: "マイクにアクセスできません"

**API通信エラー**
- HTTP 401: "API認証に失敗しました"
- HTTP 429: "リクエスト制限に達しました"
- HTTP 500: "サーバーエラーが発生しました"
- Timeout: "タイムアウトしました"

### 権限管理

**AndroidManifest.xml**
```xml
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.INTERNET" />
```

**実行時権限リクエスト (API 23+)**
```csharp
// MainActivity.OnCreate() で実行
if (CheckSelfPermission(Manifest.Permission.RecordAudio) != Permission.Granted)
{
    RequestPermissions(new[] { Manifest.Permission.RecordAudio }, REQUEST_RECORD_AUDIO);
}
```

### データフロー

```
[User Input] → [VoiceInputService] → [ConversationService]
                                             ↓
                                      [OpenAIService]
                                             ↓
[UI Update] ← [ChatFragment] ← [ConversationService]
```

### 設定項目 (将来実装)

```csharp
public class AppSettings
{
    public string OpenAIApiKey { get; set; }
    public string ModelName { get; set; } = "gpt-4";
    public double Temperature { get; set; } = 0.7;
    public string Language { get; set; } = "ja-JP";
    public bool AutoSend { get; set; } = true;
}
```

### セキュリティ考慮事項

1. **API キー管理**
   - ハードコードしない
   - 環境変数またはSecure Storage使用
   - ビルド時にobfuscation適用

2. **ネットワーク通信**
   - HTTPS必須
   - 証明書ピンニング (オプション)
   - タイムアウト設定

3. **音声データ**
   - デバイス内でのみ処理
   - 一時ファイル作成時は削除を保証

### パフォーマンス考慮

1. **RecyclerView最適化**
   - ViewHolderパターン使用
   - DiffUtil による差分更新

2. **非同期処理**
   - async/await パターン
   - ConfigureAwait(false) 使用

3. **メモリ管理**
   - 画像キャッシュ (将来実装時)
   - 履歴メッセージ数制限

### テスト戦略

1. **単体テスト**
   - VoiceInputService
   - OpenAIService
   - ConversationService

2. **統合テスト**
   - API通信フロー
   - 音声認識フロー

3. **UIテスト**
   - ナビゲーション動作
   - メッセージ表示

## 実装フェーズ

### Phase 1: 基本UI構築
- DrawerLayout実装
- ChatFragment実装
- RecyclerView + Adapter

### Phase 2: 音声入力機能
- VoiceInputService実装
- 権限管理
- UI統合

### Phase 3: OpenAI統合
- OpenAIService実装
- HTTPクライアント設定
- エラーハンドリング

### Phase 4: 統合とテスト
- 全機能統合
- エンドツーエンドテスト
- UI/UX調整

## 依存関係

### NuGetパッケージ (検討中)

```xml
<!-- HTTP通信 -->
<PackageReference Include="System.Net.Http.Json" Version="8.0.0" />

<!-- JSON処理 -->
<PackageReference Include="System.Text.Json" Version="8.0.0" />

<!-- 依存性注入 (オプション) -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
```

## 参考資料

- [Android SpeechRecognizer](https://developer.android.com/reference/android/speech/SpeechRecognizer)
- [OpenAI API Documentation](https://platform.openai.com/docs/api-reference)
- [.NET Android Documentation](https://learn.microsoft.com/dotnet/android/)
- [Material Design Guidelines](https://m3.material.io/)
