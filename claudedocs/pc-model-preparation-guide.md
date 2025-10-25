# PC Model Preparation Guide

PC上でSherpa-ONNXモデルをダウンロード・展開し、手動でデバイスにセットアップするためのガイドです。

## 概要

`scripts/prepare-models.ps1` は、PC側でモデルの準備を完全に行うスクリプトです。デバイスへの転送は含まれず、準備されたファイルを手動でセットアップします。

**このアプローチの利点:**
- デバイス側でのダウンロード不要（通信量節約）
- 複数デバイスへの展開が容易
- オフライン環境でのセットアップ可能
- ファイルの事前検証が可能

## 前提条件

- **Windows PowerShell** 5.1以降
- **tar** ユーティリティ（Windows 10+は標準搭載）
- **~500MB の空き容量**（PC上）

デバイス転送時に必要（オプション）:
- **USB ケーブル**または**Android SDK Platform Tools (adb)**

## 基本的な使い方

### インタラクティブモード

```powershell
# メニュー表示してモデル選択
.\scripts\prepare-models.ps1

# 利用可能なモデル一覧表示
.\scripts\prepare-models.ps1 -ListModels

# キャッシュクリーンアップ（.tar.bz2ファイル削除、展開済みは保持）
.\scripts\prepare-models.ps1 -CleanCache
```

### コマンドラインモード

```powershell
# 特定モデルを準備
.\scripts\prepare-models.ps1 -ModelType "sense-voice-ja-zh-en"

# カスタム出力先を指定
.\scripts\prepare-models.ps1 -ModelType "zipformer-ja-reazonspeech" -OutputDir "C:\MyModels"

# すべてのモデルを準備（バッチ処理）
.\scripts\prepare-models.ps1 -ModelType "sense-voice-ja-zh-en"
.\scripts\prepare-models.ps1 -ModelType "zipformer-ja-reazonspeech"
.\scripts\prepare-models.ps1 -ModelType "zipformer-en-2023"
.\scripts\prepare-models.ps1 -ModelType "whisper-tiny-en"
```

## 利用可能なモデル

### 1. SenseVoice Multilingual (int8)
- **Model ID**: `sense-voice-ja-zh-en`
- **サイズ**: ~227MB
- **言語**: 日本語、中国語、英語、韓国語、広東語
- **ファイル**: `model.int8.onnx`, `tokens.txt`
- **推奨用途**: 多言語対応、汎用的な用途
- **現在のRobinアプリのデフォルト**

### 2. Zipformer Japanese ReazonSpeech
- **Model ID**: `zipformer-ja-reazonspeech`
- **サイズ**: ~140MB
- **言語**: 日本語のみ
- **ファイル**: `encoder-*.onnx`, `decoder-*.onnx`, `joiner-*.onnx`, `tokens.txt`
- **推奨用途**: 日本語特化アプリ、高精度が必要な場合

### 3. Zipformer English 2023
- **Model ID**: `zipformer-en-2023`
- **サイズ**: ~66MB
- **言語**: 英語のみ
- **ファイル**: `encoder-*.onnx`, `decoder-*.onnx`, `joiner-*.onnx`, `tokens.txt`
- **推奨用途**: 英語専用アプリ、中サイズ

### 4. Whisper Tiny English
- **Model ID**: `whisper-tiny-en`
- **サイズ**: ~39MB
- **言語**: 英語のみ
- **ファイル**: `tiny.en-encoder.onnx`, `tiny.en-decoder.onnx`, `tokens.txt`
- **推奨用途**: リソース制約のあるデバイス、最速の推論

## 準備プロセス

### Step 1: ダウンロード

スクリプトはGitHub Releasesから自動的にモデルをダウンロードします。

```
Downloading: SenseVoice Multilingual (int8)
Source: https://github.com/k2-fsa/sherpa-onnx/releases/...
Size: ~227MB
✓ Download complete
```

**保存先**: `models-prepared/` ディレクトリ（デフォルト）

### Step 2: 展開

tar.bz2アーカイブを自動展開します。

```
Extracting archive...
✓ Extraction complete
```

### Step 3: 検証

モデルファイルの存在と完全性を確認します。

```
Verifying model files...
  ✓ model.int8.onnx (227.45 MB)
  ✓ tokens.txt (0.39 MB)
✓ All model files verified
```

## デバイスへのセットアップ方法

準備されたモデルを実機に配置する3つの方法：

### オプション1: APKにバンドル（推奨：標準配布）

プロジェクトに組み込んでビルドに含める方法。

```bash
# 1. プロジェクトにコピー
cp -r models-prepared/sherpa-onnx-sense-voice-* src_dotnet/Robin/Resources/raw/

# 2. Robin.csproj に追加（手動編集）
# <AndroidAsset Include="Resources\raw\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09\*.onnx">
#   <Link>assets\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09\%(Filename)%(Extension)</Link>
# </AndroidAsset>
# <AndroidAsset Include="Resources\raw\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09\tokens.txt">
#   <Link>assets\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09\tokens.txt</Link>
# </AndroidAsset>

# 3. リビルド
dotnet build src_dotnet/Robin/Robin.csproj
```

**メリット:**
- インストール後すぐに利用可能
- ネットワーク不要
- ユーザー操作不要

**デメリット:**
- APKサイズが大きくなる（+227MB）
- モデル切り替えに再ビルド必要

### オプション2: USB経由で手動転送（推奨：開発・テスト）

USBケーブルでファイルマネージャー経由で転送。

```
1. デバイスをUSBで接続
2. PCのファイルマネージャーでデバイスの内部ストレージを開く
3. Download/sherpa-models/ フォルダを作成
4. models-prepared/sherpa-onnx-* フォルダをコピー
5. Robinアプリの設定でモデルパスを指定
```

**メリット:**
- 簡単で直感的
- adb不要
- 非技術者でも可能

**デメリット:**
- 手動操作が必要
- 複数デバイスへの展開が面倒

### オプション3: adb経由で転送（推奨：自動化・大量展開）

adbコマンドで自動転送。

```powershell
# デバイス接続確認
adb devices

# モデルをプッシュ
adb push models-prepared/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09 /sdcard/Download/sherpa-models/

# 検証
adb shell "ls -lh /sdcard/Download/sherpa-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09"
```

**メリット:**
- スクリプト化・自動化可能
- 複数デバイスへの一括展開
- 高速転送（USB 3.0）

**デメリット:**
- adbのセットアップ必要
- USBデバッグ有効化必要

## ストレージ構成

### PC側（準備後）

```
robin-a-rubber-duck/
├── models-prepared/                          # デフォルト出力先
│   ├── sherpa-onnx-sense-voice-*.tar.bz2    # ダウンロードアーカイブ
│   ├── sherpa-onnx-sense-voice-*/           # 展開済みモデル
│   │   ├── model.int8.onnx                  # メインモデルファイル
│   │   └── tokens.txt                        # トークン定義
│   ├── sherpa-onnx-zipformer-ja-*/
│   │   ├── encoder-*.onnx
│   │   ├── decoder-*.onnx
│   │   ├── joiner-*.onnx
│   │   └── tokens.txt
│   └── [その他のモデル]/
```

### デバイス側（転送後）

```
Internal Storage/
└── Download/
    └── sherpa-models/
        ├── sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/
        │   ├── model.int8.onnx
        │   └── tokens.txt
        ├── sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01/
        │   ├── encoder-epoch-99-avg-1.int8.onnx
        │   ├── decoder-epoch-99-avg-1.int8.onnx
        │   ├── joiner-epoch-99-avg-1.int8.onnx
        │   └── tokens.txt
        └── [その他のモデル]/
```

## スクリプト出力例

### 正常なダウンロードと準備

```
=== Sherpa-ONNX Model Preparation Tool ===
PC-side preparation for manual device setup

=== Available Sherpa-ONNX Models ===

[1] SenseVoice Multilingual (int8)
    ID: sense-voice-ja-zh-en
    Size: ~227MB
    Languages: Japanese, Chinese, English, Korean, Cantonese
    Files: model.int8.onnx, tokens.txt

[2] Zipformer Japanese ReazonSpeech
    ID: zipformer-ja-reazonspeech
    Size: ~140MB
    Languages: Japanese only
    Files: encoder-epoch-99-avg-1.int8.onnx, decoder-epoch-99-avg-1.int8.onnx, joiner-epoch-99-avg-1.int8.onnx, tokens.txt

[A] Prepare all models
[Q] Quit

Select model number or option: 1

=== Preparing: SenseVoice Multilingual (int8) ===

[Step 1/3] Downloading model...
Downloading: SenseVoice Multilingual (int8)
Source: https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09.tar.bz2
Size: ~227MB
✓ Download complete

[Step 2/3] Extracting archive...
✓ Extraction complete

[Step 3/3] Verifying model files...
  ✓ model.int8.onnx (227.45 MB)
  ✓ tokens.txt (0.39 MB)
✓ All model files verified

=== Manual Setup Instructions ===

Model prepared at:
  C:\git\git-vo\robin-a-rubber-duck\models-prepared\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09

Option 1: Copy to project for APK bundling
  1. Copy folder to: src_dotnet\Robin\Resources\raw\
  2. Add to Robin.csproj as AndroidAsset with <Link>
  3. Rebuild app

Option 2: Manual device transfer (USB)
  1. Connect device via USB
  2. Copy folder to: Internal Storage\Download\sherpa-models\
  3. Configure model path in Robin app settings

Option 3: Deploy via adb
  adb push "C:\git\git-vo\robin-a-rubber-duck\models-prepared\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09" /sdcard/Download/sherpa-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09

=== Preparation complete! ===

=== Prepared Models Summary ===
  ✓ SenseVoice Multilingual (int8) (227.84 MB)

Total: 1 models, 227.84 MB

Storage location: C:\git\git-vo\robin-a-rubber-duck\models-prepared
```

### モデル一覧表示

```
.\scripts\prepare-models.ps1 -ListModels

=== Available Sherpa-ONNX Models ===

[1] SenseVoice Multilingual (int8)
    ID: sense-voice-ja-zh-en
    Size: ~227MB
    Languages: Japanese, Chinese, English, Korean, Cantonese

[2] Zipformer Japanese ReazonSpeech
    ID: zipformer-ja-reazonspeech
    Size: ~140MB
    Languages: Japanese only

[3] Zipformer English 2023
    ID: zipformer-en-2023
    Size: ~66MB
    Languages: English only

[4] Whisper Tiny English
    ID: whisper-tiny-en
    Size: ~39MB
    Languages: English only

=== Prepared Models Summary ===
  ✓ SenseVoice Multilingual (int8) (227.84 MB)
  ✓ Zipformer Japanese ReazonSpeech (140.23 MB)

Total: 2 models, 368.07 MB

Storage location: C:\git\git-vo\robin-a-rubber-duck\models-prepared
```

## トラブルシューティング

### "tar: command not found"

**原因**: tar ユーティリティが見つからない

**解決策**:
```powershell
# Windows 10+ではtarが標準搭載
where.exe tar

# ない場合はGit for Windowsをインストール
# または7-Zipで手動展開
```

### ダウンロードが失敗する

**原因**: ネットワークエラーまたはGitHub接続問題

**解決策**:
```powershell
# リトライ（スクリプトは既存ファイルをスキップ）
.\scripts\prepare-models.ps1 -ModelType "sense-voice-ja-zh-en"

# 手動ダウンロード
# 1. ブラウザでURLを開く（スクリプト出力に表示）
# 2. models-prepared/ に保存
# 3. 手動展開: tar -xjf *.tar.bz2
```

### ディスク容量不足

**原因**: PC側のストレージ不足

**解決策**:
```powershell
# 使用済みアーカイブを削除
.\scripts\prepare-models.ps1 -CleanCache

# カスタム出力先（別ドライブ）を指定
.\scripts\prepare-models.ps1 -ModelType "..." -OutputDir "D:\Models"
```

### モデルファイルの検証エラー

**原因**: ダウンロード不完全またはアーカイブ破損

**解決策**:
```powershell
# 該当モデルフォルダとアーカイブを削除
Remove-Item -Recurse models-prepared/sherpa-onnx-*

# 再ダウンロード
.\scripts\prepare-models.ps1 -ModelType "..."
```

## パフォーマンスと容量

### ダウンロード時間

| 回線速度 | SenseVoice (227MB) | Zipformer JA (140MB) | Whisper Tiny (39MB) |
|---------|-------------------|---------------------|---------------------|
| 100 Mbps | ~20秒 | ~12秒 | ~3秒 |
| 50 Mbps | ~40秒 | ~24秒 | ~7秒 |
| 10 Mbps | ~3分 | ~2分 | ~30秒 |
| 5 Mbps | ~6分 | ~4分 | ~1分 |

### ストレージ要件

**PC側（models-prepared/）:**
- アーカイブ + 展開済み = 約2倍のサイズ
- `-CleanCache` でアーカイブ削除可能

**デバイス側:**
- モデルのみ（アーカイブ不要）
- オプション1（APKバンドル）: アプリ内蔵、追加容量不要
- オプション2/3（外部ストレージ）: Download/に配置

### 転送時間（USB経由）

| 接続 | SenseVoice (227MB) | Zipformer JA (140MB) |
|-----|-------------------|---------------------|
| USB 3.0 | ~10秒 | ~6秒 |
| USB 2.0 | ~30秒 | ~18秒 |
| USB 1.1 | ~3分 | ~2分 |

## ワークフロー例

### 開発環境でのテスト

```powershell
# 1. PC側準備
.\scripts\prepare-models.ps1 -ModelType "zipformer-ja-reazonspeech"

# 2. デバイスに転送（adb）
adb push models-prepared/sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01 /sdcard/Download/sherpa-models/

# 3. アプリで設定
# Settings → Model Path → /sdcard/Download/sherpa-models/sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01

# 4. 認識テスト
# マイクボタンで音声入力
```

### 本番APK作成

```bash
# 1. PC側準備
.\scripts\prepare-models.ps1 -ModelType "sense-voice-ja-zh-en"

# 2. プロジェクトにコピー
cp -r models-prepared/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09 src_dotnet/Robin/Resources/raw/

# 3. csprojに追加（手動編集）
# AndroidAsset Include + Link設定

# 4. リリースビルド
dotnet build -c Release src_dotnet/Robin/Robin.csproj

# 5. APK署名・配布
```

### 複数モデルの比較テスト

```powershell
# すべてのモデルを準備
.\scripts\prepare-models.ps1 -ModelType "sense-voice-ja-zh-en"
.\scripts\prepare-models.ps1 -ModelType "zipformer-ja-reazonspeech"
.\scripts\prepare-models.ps1 -ModelType "whisper-tiny-en"

# デバイスに一括転送
adb push models-prepared/sherpa-onnx-sense-voice-* /sdcard/Download/sherpa-models/
adb push models-prepared/sherpa-onnx-zipformer-ja-* /sdcard/Download/sherpa-models/
adb push models-prepared/sherpa-onnx-whisper-* /sdcard/Download/sherpa-models/

# アプリで切り替えてテスト
# 精度、速度、メモリ使用量を比較
```

## 将来の改善計画

### ランタイムダウンロード機能（計画中）

`ModelDownloadService.cs` 実装により：
- アプリ内でモデル選択・ダウンロード
- 動的なモデル切り替え
- APKサイズの削減（モデル非同梱）

詳細: `claudedocs/runtime-download-implementation.md`

### 差分更新機能（計画中）

- モデルバージョン管理
- 差分ダウンロード（ONNX layerレベル）
- キャッシュ管理とクリーンアップ

## 関連ドキュメント

- `claudedocs/sherpa-onnx-setup.md` - Sherpa-ONNX初期セットアップ
- `claudedocs/sherpa-onnx-integration.md` - 統合ガイド
- `claudedocs/runtime-download-implementation.md` - ランタイムダウンロード計画
- `claudedocs/dotnet-android-assets-guide.md` - Asset パッケージング詳細
- [Sherpa-ONNX Models](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html) - 公式モデル一覧

## FAQ

**Q: すべてのモデルを準備する必要がありますか？**

A: いいえ。必要なモデルのみ準備してください。開発初期はSenseVoice（多言語）、日本語特化ならZipformer JA推奨。

**Q: アーカイブファイル（.tar.bz2）は削除してよいですか？**

A: はい。`-CleanCache` で削除できます。展開済みモデルは保持されます。

**Q: カスタムモデルを追加できますか？**

A: スクリプトの `$Models` ハッシュテーブルを編集してURLとファイル情報を追加すれば可能です。

**Q: 複数のPCで共有できますか？**

A: `models-prepared/` フォルダをネットワーク共有または外部ドライブに配置し、`-OutputDir` で指定すれば共有可能。

**Q: モデル更新時はどうすればよいですか？**

A: 古いモデルフォルダを削除してスクリプトを再実行すると、新バージョンがダウンロードされます。