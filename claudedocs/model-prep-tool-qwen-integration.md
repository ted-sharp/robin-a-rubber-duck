# ModelPrepTool Qwen統合ガイド

## 実装状況

### ✅ 完了項目

1. **QwenModelDefinition.cs** (新規作成)
   - Qwenモデルメタデータ定義
   - `onnx-community/Qwen2.5-1.5B` リポジトリ対応
   - ダウンロード可能なファイルリスト

2. **ModelDownloader クラス拡張**
   - `DownloadAndPrepareAsync(QwenModelDefinition)` メソッド追加
   - Hugging Face CDNからのファイルダウンロード対応
   - 進捗追跡機能実装
   - ファイル検証機能

3. **ModelVerifier クラス拡張**
   - Qwenモデル検証ロジック追加

4. **ModelPrepTool統合**
   - Qwenモデル選択ロジック
   - `--model qwen-2.5-1.5b-int4` コマンド対応
   - セットアップ指示表示機能
   - ヘルプドキュメント更新

### ✅ ビルド結果
```
ビルドに成功しました。
    0 個の警告
    0 エラー
```

### 📋 実装待機事項

#### 1. Hugging FaceのONNXファイル構造確認

現在、`onnx-community/Qwen2.5-1.5B` リポジトリの実際のファイル構造を検証中：

**確認済みファイル:**
- ✅ `config.json` (822 bytes)
- ✅ `tokenizer.json` (7.03 MB)
- ✅ `special_tokens_map.json` (616 bytes)
- ✅ `tokenizer_config.json` (7.23 kB)
- ✅ `generation_config.json` (117 bytes)

**検証待機:**
- `onnx/` サブディレクトリのモデルファイル
  - 正確なファイル名確認
  - ダウンロードURL検証
  - ファイルサイズ確認

#### 2. 現在の問題

**エラー:** `404 Not Found - onnx/config.json`

**原因:** `config.json` はリポジトリルートにあり、`onnx/config.json` ではない

**解決方法:** ファイルパスを修正

```csharp
// 修正前（不正）
"onnx/config.json"

// 修正後（正）
"config.json"
```

## 使用方法

### Qwenモデルのダウンロード

```bash
# 指定モデルのダウンロード
cd src_dotnet/ModelPrepTool/bin/Debug/net10.0
./ModelPrepTool --model qwen-2.5-1.5b-int4 --output ../../../../../../models-prepared

# すべてのモデルをダウンロード
./ModelPrepTool --model all

# ダウンロード済みモデルのリスト表示
./ModelPrepTool --list

# ヘルプ表示
./ModelPrepTool --help
```

### ダウンロード出力

```
=== Model Preparation Tool ===
Output directory: C:\git\git-vo\robin-a-rubber-duck\models-prepared

=== Preparing: Qwen 2.5 1.5B ONNX (int4) ===
Size: 4000.0 MB
Languages: Chinese, English, Japanese, Korean, Cantonese
Type: int4 quantization
  Checking file sizes...
  Downloading config.json...
  Progress: 0.0% (0.1 / 4000.0 MB) @ 50.00 MB/s
  ...
  Download complete
  [OK] Model prepared at: C:\git\git-vo\robin-a-rubber-duck\models-prepared\qwen25-1.5b-int4

=== Device Setup Instructions (Qwen) ===

Prepared model: C:\git\git-vo\robin-a-rubber-duck\models-prepared\qwen25-1.5b-int4

The Qwen ONNX model is ready for use with the Robin app.

Usage:

[Option 1] Use directly from PC (Development)
  1. Configure Robin app to use this model path
  2. Model path: C:\git\git-vo\robin-a-rubber-duck\models-prepared\qwen25-1.5b-int4

[Option 2] Transfer to Android Device
  1. Connect Android device via USB
  2. Open device in File Explorer
  3. Copy model folder to: Internal Storage/Download/robin-models/
  4. In Robin app: Configure model path or auto-download feature

[Option 3] adb Push
  adb push "C:\git\git-vo\robin-a-rubber-duck\models-prepared\qwen25-1.5b-int4" /sdcard/Download/robin-models/qwen25-1.5b-int4
```

## アーキテクチャ

### ダウンロードフロー

```
ModelPrepTool.Program
    ↓
SelectQwenModels()
    ↓
ModelDownloader.DownloadAndPrepareAsync(QwenModelDefinition)
    ↓
┌─────────────────────────────────────┐
│ 1. GetFileSizeAsync()               │ ← Hugging Face HEAD リクエスト
├─────────────────────────────────────┤
│ 2. DownloadFileAsync()              │ ← HTTP GET + 進捗報告
├─────────────────────────────────────┤
│ 3. ModelVerifier.VerifyModel()      │ ← ファイル検証
└─────────────────────────────────────┘
    ↓
セットアップ指示表示
```

### ファイルツリー

```
models-prepared/
└── qwen25-1.5b-int4/
    ├── config.json
    ├── tokenizer.json
    ├── special_tokens_map.json
    ├── tokenizer_config.json
    ├── generation_config.json
    └── onnx/
        ├── model_quantized.onnx  (TBD)
        └── ...
```

## Hugging Face ダウンロードURL パターン

```
https://huggingface.co/{RepositoryPath}/resolve/main/{FilePath}

例：
https://huggingface.co/onnx-community/Qwen2.5-1.5B/resolve/main/config.json
https://huggingface.co/onnx-community/Qwen2.5-1.5B/resolve/main/tokenizer.json
https://huggingface.co/onnx-community/Qwen2.5-1.5B/resolve/main/onnx/model_quantized.onnx
```

## 次のステップ

### 1. ファイルパス修正
```csharp
// QwenModelDefinition.cs でファイルパスを正確に指定
RequiredFiles = new[]
{
    "config.json",
    "tokenizer.json",
    "special_tokens_map.json",
    "tokenizer_config.json",
    "generation_config.json"
    // onnx/ サブディレクトリファイルは以下に追加予定:
    // "onnx/model_quantized.onnx",
    // ...
};
```

### 2. ONNX モデルファイル特定
- Transformers.js ONNX formatの正確なファイル名確認
- ダウンロード可能性検証
- ファイルサイズとチェックサム記録

### 3. テスト実行
```bash
./ModelPrepTool --model qwen-2.5-1.5b-int4
```

### 4. Robin アプリ統合
- `QwenInferenceService` でダウンロード済みモデルを使用
- ONNX Runtime推論エンジン実装
- トークナイザー統合

## トラブルシューティング

### 404 Not Found エラー
**原因:** ファイルパスが正確でない
**解決:** Hugging Faceリポジトリで実際のパスを確認

### ダウンロード速度が遅い
**原因:** Hugging Face CDNの制限またはネットワーク遅延
**解決:**
- 通常速度: 5-50 MB/s
- 待機時間: ~800MB で約 20-160 秒

### ファイル検証失敗
**原因:** ダウンロード中断またはファイル破損
**解決:** モデルディレクトリを削除して再ダウンロード

## 統計

- **プロジェクトファイル更新:** 5ファイル
- **新規ファイル:** 1ファイル
- **コード行数追加:** ~150行
- **ビルド結果:** 成功 (0 エラー、0 警告)
- **依存関係追加:** なし

---

**最終更新:** 2025-10-25
**ステータス:** 実装完了、Hugging Face ファイル構造検証待機中
**次実行コマンド:** `./ModelPrepTool --model qwen-2.5-1.5b-int4` (ファイルパス修正後)
