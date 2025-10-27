# Android LM Studio接続 デバッグガイド

## 概要

このガイドでは、AndroidアプリからLM Studioへの接続を診断・デバッグする方法を説明します。

## 診断ステップ

### ステップ1: PC側の確認（既完了）

```bash
# netstatでポート1234をリッスンしているか確認
netstat -an | findstr ":1234"
# 出力例: TCP    0.0.0.0:1234  0.0.0.0:0     LISTENING

# ピングで疎通確認
ping 192.168.0.7
```

### ステップ2: Android側のネットワーク確認

```bash
# Androidからホストへpingテスト
adb shell ping 192.168.0.7 -c 4

# DNSの確認（オプション）
adb shell getprop net.dns1
```

### ステップ3: Android側のログ確認

```bash
# SettingsActivityの接続テストログを確認
adb logcat -c  # ログクリア
adb logcat SettingsActivity:* OpenAIService:* Robin:* *:E
```

接続テスト時に以下の情報がlogcatに表示されます：

```
SettingsActivity: 接続テスト開始 - URL: http://192.168.0.7:1234/v1/chat/completions
SettingsActivity: 接続テスト結果 - Status: 200
OpenAIService: 初期化完了 - BaseAddress: http://192.168.0.7:1234/v1/, Model: mistral, IsLMStudio: true
OpenAIService: リクエスト開始 - URL: http://192.168.0.7:1234/v1/chat/completions, メッセージ数: 1
OpenAIService: レスポンス受信 - Status: 200
OpenAIService: レスポンス解析成功: こんにちは...
```

## 問題診断フロー

### 問題：接続テストが失敗する

#### a. タイムアウトエラー
```
SettingsActivity: 接続テストタイムアウト
```

**原因：**
- LM Studioが起動していない
- ファイアウォールがポート1234をブロック
- ネットワークが断絶

**確認方法：**
```bash
# PC側でLM Studio状態確認
netstat -an | findstr ":1234"

# AndroidからPCへping
adb shell ping 192.168.0.7 -c 4

# Windowsファイアウォール設定確認
# コントロールパネル → セキュリティ → Windows Defender ファイアウォール
#  → 詳細設定 → 受信規則 → ポート1234を確認
```

#### b. HTTP接続エラー
```
SettingsActivity: 接続エラー: HttpRequestException
```

**原因：**
- URLが無効（HTTPSとHTTPの混在など）
- LM Studioエンドポイントが異なる

**確認方法：**
```bash
# SettingsActivityで入力したURLが正しいか確認
# 形式: http://192.168.0.7:1234 （HTTPSではなくHTTP）

# curlでテスト（PC側）
curl -X POST http://192.168.0.7:1234/v1/chat/completions ^
  -H "Content-Type: application/json" ^
  -d "{\"model\":\"mistral\",\"messages\":[{\"role\":\"user\",\"content\":\"test\"}]}"
```

#### c. モデル名エラー
```
OpenAIService: レスポンス受信 - Status: 404
```

**原因：**
- SettingsActivityで入力したモデル名がLM Studioに存在しない

**確認方法：**
```bash
# LM Studioで実際にロードされているモデルを確認
# LM Studio UI → Models タブで現在のモデルを確認

# 或いはAPIで確認（PC側）
curl http://192.168.0.7:1234/v1/models
```

### 問題：設定画面で接続テストボタンを押しても反応がない

**原因：**
- UIスレッドが応答していない
- TimeoutSpanが短すぎる（現在は10秒）

**解決方法：**
```csharp
// SettingsActivity.cs の TestLMStudioConnection() メソッドで
// TimeoutSpanを増やす（10秒 → 30秒）
var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
```

### 問題：接続テストは成功するが、マイク入力後のLLMレスポンスが来ない

**原因：**
- OpenAIService.SendMessageAsync() で異なるエラーが発生

**確認方法：**
```bash
# マイク入力後のログを確認
adb logcat | grep -E "OpenAIService|MainActivity"

# 以下のログパターンを確認：
# ❌ "HTTP通信エラー" → エンドポイント設定確認
# ❌ "タイムアウト" → LM Studio応答遅延
# ❌ "APIエラー" → モデル名やリクエスト形式の問題
```

## logcatフィルタリング

### 全ログを見る
```bash
adb logcat
```

### 特定のタグだけを見る
```bash
adb logcat OpenAIService:I SettingsActivity:I Robin:I *:E

# ログレベル：
# V = Verbose
# D = Debug
# I = Info
# W = Warning
# E = Error
# F = Fatal
```

### ログをファイルに保存
```bash
adb logcat > robin_debug.log &

# 後で確認
cat robin_debug.log | grep OpenAIService
```

## 追加デバッグ情報

### OpenAIService クラスのログポイント

| ログ | 意味 | 正常値 |
|------|------|--------|
| `初期化完了 - BaseAddress: ...` | サービス初期化成功 | 含む |
| `リクエスト開始 - URL: ...` | API呼び出し開始 | 毎回出力 |
| `レスポンス受信 - Status: 200` | HTTP 200成功 | Status: 200 |
| `レスポンス解析成功: ...` | JSON解析完了 | 3行内に出力 |

### SettingsActivity クラスのログポイント

| ログ | 意味 | 正常値 |
|------|------|--------|
| `接続テスト開始 - URL: ...` | 接続テスト開始 | 含む |
| `接続テスト結果 - Status: 200` | 接続成功 | Status: 200 |
| `接続テストタイムアウト` | 接続失敗（タイムアウト） | ⚠️ トラブル |
| `接続エラー: ...` | HTTP例外発生 | ⚠️ トラブル |

## よくあるトラブルシューティング

### Q: "接続に失敗しているようです" メッセージが出る
**A:** 以下を確認してください：
1. LM Studioが起動しているか
2. ポート1234でリッスンしているか（netstat確認）
3. Androidがそのネットワークに接続しているか（同じWiFi）
4. Windowsファイアウォール設定

### Q: アプリがクラッシュする
**A:** logcatで以下を確認：
```bash
adb logcat | grep -E "Exception|Error|Crash"
```

### Q: APIレスポンスが空 ("APIからの応答が空です")
**A:** モデル名が間違っている可能性があります：
```bash
# LM Studioで実際のモデル名を確認
curl http://192.168.0.7:1234/v1/models | jq .

# 出力例：
# {
#   "object": "list",
#   "data": [
#     {
#       "id": "mistral-7b-instruct-v0.1",  ← これが正しい名前
#       "object": "model"
#     }
#   ]
# }
```

## デバッグ時の推奨設定

**開発中の設定（SettingsActivity）:**
- Endpoint: `http://192.168.0.7:1234`
- Model Name: LM Studioで実際にロードされているモデル名
- Is Enabled: チェック✓

**APIタイムアウト設定：**
- OpenAIService: 60秒
- SettingsActivity接続テスト: 10秒

## ログ出力の一例（成功ケース）

```
I/SettingsActivity: 接続テスト開始 - URL: http://192.168.0.7:1234/v1/chat/completions
I/SettingsActivity: 接続テスト結果 - Status: 200
I/SettingsActivity: ✅ LM Studioへの接続確認成功
D/OpenAIService: リクエストボディ作成完了
I/OpenAIService: 初期化完了 - BaseAddress: http://192.168.0.7:1234/v1/, Model: mistral, IsLMStudio: true
I/OpenAIService: リクエスト開始 - URL: http://192.168.0.7:1234/v1/chat/completions, メッセージ数: 1
I/OpenAIService: レスポンス受信 - Status: 200
D/OpenAIService: レスポンス内容: {"id":"chatcmpl-...
I/OpenAIService: レスポンス解析成功: こんにちは！何かお力になれることはありますか？
```

## 次のステップ

1. ✅ PC側: LM Studio起動 + netstat確認
2. ✅ ネットワーク: ping確認
3. ⏳ Android: SettingsActivityで接続テスト
4. ⏳ logcat: 接続テストのログ確認
5. ⏳ マイク: 音声入力後の動作確認

---
最後更新: 2025-10-26
