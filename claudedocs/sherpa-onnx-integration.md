# Sherpa-ONNX Android統合ガイド

## 概要

Sherpa-ONNXは、次世代Kaldiとonnxruntimeを使用した、完全オフラインの音声認識・音声合成エンジンです。インターネット接続なしで、Android、iOS、組み込みシステムなど様々なプラットフォームで動作します。

### 主な特徴

- **完全オフライン**: インターネット接続不要、すべてローカルで処理
- **真のストリーミング対応**: 切れ目のないリアルタイム音声認識
- **軽量**: モデルサイズ 20-40MB（日本語対応）
- **高精度**: 最新のTransformerベースモデル（Zipformer、Paraformer、SenseVoice）
- **多言語対応**: 日本語を含む70以上の言語サポート
- **システム音なし**: Androidの標準SpeechRecognizerと異なり、ビープ音なし
- **マルチプラットフォーム**: 12のプログラミング言語をサポート

### 2025年時点の最新情報

- リポジトリ: https://github.com/k2-fsa/sherpa-onnx
- 公式ドキュメント: https://k2-fsa.github.io/sherpa/onnx/index.html
- アクティブ開発中、定期的に新モデルリリース

---

## 日本語対応モデル

### 1. SenseVoice マルチ言語モデル（推奨）

**モデル名**: `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09`

**対応言語**: 中国語、英語、日本語、韓国語、広東語

**特徴**:
- 5言語対応で汎用性が高い
- Int8量子化により軽量化
- 2025年9月の最新モデル
- 良好な精度とレスポンス

**ダウンロード**:
```bash
wget https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17.tar.bz2
tar xvf sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17.tar.bz2
```

**モデルサイズ**: 約240MB（圧縮後）

### 2. NEMO Parakeet 日本語専用モデル

**モデル名**: `sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8`

**対応言語**: 日本語のみ

**特徴**:
- 日本語に特化した高精度
- 0.6B パラメータ
- Int8量子化

**ダウンロード**:
```bash
wget https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8.tar.bz2
tar xvf sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8.tar.bz2
```

### 3. Zipformer 日本語モデル

**特徴**:
- Webデモあり: https://huggingface.co/spaces/k2-fsa/web-assembly-vad-asr-sherpa-onnx-ja-zipformer
- ストリーミングに最適化
- 軽量でレスポンスが早い

**すべてのモデルダウンロードページ**:
https://github.com/k2-fsa/sherpa-onnx/releases/tag/asr-models

---

## Android統合方法

### アーキテクチャ

```
Android App (.NET MAUI / Xamarin)
    ↓ JNI Interop
Java/Kotlin Wrapper
    ↓ JNI
Sherpa-ONNX Native Library (C++)
    ↓
ONNXRuntime + Model Files
```

### 必要なライブラリファイル

#### 1. ネイティブ共有ライブラリ（.so）

各ABIごとに以下が必要:
- `libsherpa-onnx-jni.so` (約2-4MB) - JNIバインディング
- `libonnxruntime.so` (約15-17MB) - ONNX Runtime

**配置場所**: `app/src/main/jniLibs/[ABI]/`

**対応ABI**:
- `arm64-v8a` (64bit ARM - 現代のほとんどのAndroid端末)
- `armeabi-v7a` (32bit ARM - 古い端末向け)
- `x86_64` (エミュレータ用)
- `x86` (古いエミュレータ用)

#### 2. Java/Kotlinライブラリ

Maven Central経由で入手可能:
```gradle
implementation 'org.k2fsa.sherpa:onnx:1.10.17'
```

または、AARファイルを直接ダウンロード:
https://github.com/k2-fsa/sherpa-onnx/releases

#### 3. モデルファイル

**配置場所**: `app/src/main/assets/`

**モデル構成ファイル**:
- `model.onnx` (または `encoder.onnx`, `decoder.onnx`, `joiner.onnx`)
- `tokens.txt` - トークン定義ファイル
- 各種設定ファイル（モデルによって異なる）

**APKサイズ最適化**:
- テスト用WAVファイルを削除
- READMEファイルを削除
- 不要なモデルバリエーション（int8以外）を削除

---

## .NET MAUI / Xamarin統合の課題と解決策

### 既知の問題（2025年時点）

**GitHub Issue #1241**: Sherpa-ONNXのC# Xamarin Androidバインディングには問題があります。

**問題内容**:
- `DllNotFoundException` が発生（`libsherpa-onnx-c-api.so`が見つからない）
- 原因: 推移的依存関係 `libpthread.so.0` がAndroidに存在しない
- WindowsとLinuxでは動作するが、Android上では失敗

### 解決策: Javaバインディング経由での統合

.NET MAUIでは、AndroidのJavaライブラリを直接利用できます。

#### 統合アプローチ

**1. Javaバインディングライブラリプロジェクトを作成**

```xml
<!-- Bindings.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- AARファイルを組み込む -->
    <AndroidLibrary Include="sherpa-onnx-*.aar" />
  </ItemGroup>
</Project>
```

**2. ネイティブライブラリを埋め込む**

```xml
<!-- Robin.csproj に追加 -->
<ItemGroup>
  <EmbeddedNativeLibrary Include="libs\arm64-v8a\libsherpa-onnx-jni.so" />
  <EmbeddedNativeLibrary Include="libs\arm64-v8a\libonnxruntime.so" />
</ItemGroup>
```

**3. C#からJava APIを呼び出す**

```csharp
using Android.Runtime;
using Java.Interop;

// Javaクラスを直接利用
var recognizer = new Org.K2fsa.Sherpa.Onnx.OnlineRecognizer(...);
```

#### メリット

- ✅ AndroidネイティブのJava APIを直接使用
- ✅ C#バインディングの問題を回避
- ✅ 公式のAndroidサポートを活用
- ✅ 既存のKotlin/Javaサンプルを参考にできる

#### デメリット

- ⚠️ Java.Interop経由で若干冗長
- ⚠️ C#の型システムとJavaの型システムの変換が必要

---

## 実装例（Kotlin/Java API）

### 基本的な使用例

```kotlin
import org.k2fsa.sherpa.onnx.*

// 設定の準備
val config = OnlineRecognizerConfig(
    featConfig = getFeatureConfig(sampleRate = 16000, featureDim = 80),
    modelConfig = getModelConfig(type = 0)!!, // zipformer
    decodingConfig = OnlineRecognizerDecodingConfig(method = "greedy_search"),
    enableEndpoint = true
)

// 認識器の作成
val recognizer = OnlineRecognizer(
    assetManager = context.assets,
    config = config
)

// ストリームの作成
val stream = recognizer.createStream()

// 音声データの送信（リアルタイム）
val samples = FloatArray(1600) // 100ms @ 16kHz
stream.acceptWaveform(samples, sampleRate = 16000)

// 結果の取得
while (recognizer.isReady(stream)) {
    recognizer.decode(stream)
}

val text = recognizer.getResult(stream).text
```

### VAD（音声検出）との連携

```kotlin
// VAD設定
val vadConfig = SileroVadModelConfig(
    model = "silero_vad.onnx",
    minSilenceDuration = 0.5f,
    minSpeechDuration = 0.25f,
    threshold = 0.5f
)

val vad = VoiceActivityDetector(vadConfig, bufferSizeInSeconds = 60)

// 音声区間の検出
vad.acceptWaveform(samples)
if (vad.isSpeech()) {
    // 音声区間を認識器に送信
    stream.acceptWaveform(samples, sampleRate = 16000)
}
```

---

## Robinアプリへの統合プラン

### ステップ1: プロジェクト準備

1. **Sherpa-ONNXライブラリの追加**
   - AARファイルをダウンロード
   - Bindingsプロジェクトを作成
   - Robin.csprojに参照を追加

2. **モデルファイルの配置**
   - 日本語モデル（SenseVoice推奨）をダウンロード
   - `Resources/raw/` または assets として追加
   - 不要なファイルを削除してAPKサイズ最適化

### ステップ2: サービス層の実装

新しいサービスクラス `SherpaRealtimeService.cs` を作成:

```csharp
public class SherpaRealtimeService
{
    private OnlineRecognizer _recognizer;
    private OnlineStream _stream;
    private bool _isListening;

    public event EventHandler<string> PartialResult;
    public event EventHandler<string> FinalResult;

    public void Initialize(Context context)
    {
        // Java APIを使用して初期化
        // モデルをassetsから読み込み
    }

    public void StartListening()
    {
        // AudioRecordから音声データを取得
        // リアルタイムでstreamに送信
    }

    public void StopListening()
    {
        // 認識を停止
        // 最終結果を取得
    }
}
```

### ステップ3: UI統合

既存の`MainActivity.cs`を拡張:

```csharp
private SherpaRealtimeService _sherpaService;

private void InitializeServices()
{
    // 既存のVoiceInputServiceと並行して使用
    _sherpaService = new SherpaRealtimeService();
    _sherpaService.Initialize(this);
    _sherpaService.PartialResult += OnSherpaPartialResult;
    _sherpaService.FinalResult += OnSherpaFinalResult;
}
```

### ステップ4: 設定切り替え

ユーザーが選択できるようにする:
- Android標準音声認識（既存のVoiceInputService）
- Sherpaオフライン音声認識（新しいSherpaRealtimeService）

---

## パフォーマンス考慮事項

### APKサイズ

- **ネイティブライブラリ**: 約20MB（arm64-v8aのみの場合）
- **ONNXRuntime**: 約15MB
- **日本語モデル**: 約40-240MB（モデルによる）
- **合計**: 約75-275MB の APK サイズ増加

### 実行時メモリ

- **モデルロード時**: 50-300MB（モデルサイズによる）
- **推論時**: 追加で50-100MB
- **合計**: 100-400MB のメモリ使用

### CPU使用率

- **リアルタイム認識**: 1コア 20-40%程度
- **レイテンシ**: 100-300ms（端末性能による）
- **バッテリー影響**: 中程度（連続使用時）

### 最適化手法

1. **モデル量子化**: Int8モデルを使用（精度とサイズのバランス）
2. **ABI制限**: arm64-v8aのみをターゲットにする
3. **遅延ロード**: 必要になるまでモデルを読み込まない
4. **VAD統合**: 無音区間で処理をスキップ

---

## 開発リソース

### 公式ドキュメント

- **メインドキュメント**: https://k2-fsa.github.io/sherpa/onnx/index.html
- **Android統合ガイド**: https://k2-fsa.github.io/sherpa/onnx/android/index.html
- **Kotlin API**: https://k2-fsa.github.io/sherpa/onnx/kotlin-api/index.html

### サンプルコード

- **GitHub リポジトリ**: https://github.com/k2-fsa/sherpa-onnx
- **Androidサンプルアプリ**: https://github.com/k2-fsa/sherpa-onnx/tree/master/android
- **プリビルトAPK**: https://k2-fsa.github.io/sherpa/onnx/android/prebuilt-apk.html

### コミュニティ

- **GitHub Issues**: https://github.com/k2-fsa/sherpa-onnx/issues
- **Hugging Face デモ**: https://huggingface.co/spaces/k2-fsa/web-assembly-vad-asr-sherpa-onnx-ja-zipformer

---

## 比較: Sherpa-ONNX vs Android SpeechRecognizer

| 特徴 | Sherpa-ONNX | Android SpeechRecognizer |
|------|-------------|-------------------------|
| オフライン対応 | ✅ 完全対応 | ⚠️ 限定的（端末依存） |
| リアルタイム | ✅ 真のストリーミング | ❌ 断続的 |
| システム音 | ✅ なし | ❌ ビープ音あり |
| 切れ目 | ✅ なし | ❌ 認識ごとに途切れる |
| 精度 | ⭐⭐⭐⭐ 高い | ⭐⭐⭐⭐⭐ 非常に高い（Google API） |
| APKサイズ | ❌ +75-275MB | ✅ 増加なし |
| 実装難易度 | ⚠️ 中程度 | ✅ 簡単 |
| カスタマイズ性 | ✅ 高い | ❌ 低い |
| 依存関係 | ❌ ネイティブライブラリ必要 | ✅ システム標準 |

---

## 次のステップ

### 短期（検証フェーズ）

1. **プロトタイプ作成**
   - Kotlinでシンプルな音声認識アプリを作成
   - 日本語認識の精度を検証
   - レスポンスとレイテンシを測定

2. **.NET統合テスト**
   - Bindingsプロジェクトを作成
   - 最小限のC#ラッパーを実装
   - 基本的な認識が動作することを確認

### 中期（統合フェーズ）

3. **Robin統合**
   - `SherpaRealtimeService` の完全実装
   - UIでの切り替え機能追加
   - エラーハンドリングとリソース管理

4. **最適化**
   - APKサイズの削減
   - メモリ使用量の最適化
   - バッテリー消費の改善

### 長期（本番フェーズ）

5. **ユーザーテスト**
   - 実機での動作確認
   - 様々な環境での精度テスト
   - ユーザーフィードバック収集

6. **リリース準備**
   - ドキュメント整備
   - 設定UI実装
   - モデル選択機能（精度 vs サイズ）

---

## まとめ

Sherpa-ONNXは、Robinアプリに真のオフライン連続音声認識を実現する最適なソリューションです。

**主な利点**:
- システム音なし、切れ目なしの自然な音声認識
- 完全オフライン動作
- 高精度な日本語認識
- アクティブに開発されているコミュニティ

**課題**:
- APKサイズの増加（75-275MB）
- .NET統合にJava Interopが必要
- 実装が標準APIより複雑

**推奨アプローチ**:
1. まずKotlinでプロトタイプ検証
2. 精度とパフォーマンスを確認
3. .NET Bindingsプロジェクト作成
4. Robinに段階的に統合

このアプローチにより、高品質なオフライン音声認識機能をRobinに追加できます。
