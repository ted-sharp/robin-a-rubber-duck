# ModelPrepTool Configuration Guide

ModelPrepTool が利用可能なすべてのモデルを自動でダウンロードするのを避けるために、`model-prep-config.json` で自分の用途に合わせたモデル選択ができるようになりました。

## 設定ファイルについて

### デフォルト動作

1. `model-prep-config.json` が存在しない場合、ツールは自動的にデフォルト設定ファイルを作成します
2. デフォルト設定には、よく使われるモデルが含まれています：
   - `sense-voice-ja-zh-en` (Sherpa-ONNX)
   - `zipformer-ja-reazonspeech` (Sherpa-ONNX)
   - `qwen-2.5-0.5b` (Qwen)

### ファイルの場所

デフォルトでは、ModelPrepTool の実行ファイルと同じディレクトリに `model-prep-config.json` を探します。

```
src_dotnet/ModelPrepTool/bin/Debug/net10.0/
├── ModelPrepTool.dll
└── model-prep-config.json        ← ここに配置
```

## 設定ファイルの形式

### 基本形式

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en",
    "zipformer-ja-reazonspeech",
    "whisper-tiny",
    "qwen-2.5-0.5b"
  ]
}
```

### 利用可能なモデル ID

#### Sherpa-ONNX モデル（音声認識）

| ID | 名前 | サイズ | 言語 |
|---|---|---|---|
| `sense-voice-ja-zh-en` | SenseVoice Multilingual | 238 MB | Japanese, Chinese, English, Korean, Cantonese |
| `zipformer-ja-reazonspeech` | Zipformer Japanese ReazonSpeech | 約 150 MB | Japanese |
| `whisper-tiny` | Whisper Tiny | 約 104 MB | Multilingual |
| `nemo-parakeet-cja` | NeMo Parakeet CTC 0.6B | 約 625 MB | Japanese |

#### Qwen モデル（テキスト推論）

| ID | 名前 | サイズ |
|---|---|---|
| `qwen-2.5-0.5b` | Qwen 2.5 0.5B Instruct | 約 3 GB |
| `qwen-2.5-1.5b-int4` | Qwen 2.5 1.5B (int4) | 約 9 GB |

## 使用例

### 例1: 日本語音声認識とQwenの最小構成

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en",
    "qwen-2.5-0.5b"
  ]
}
```

**コンソール出力：**
```
[SKIP] 以下の Sherpa-ONNX モデルはスキップします:
  - zipformer-ja-reazonspeech (Zipformer Japanese ReazonSpeech)
  - whisper-tiny (Whisper Tiny (Multilingual, int8))
  - nemo-parakeet-cja (NeMo Parakeet CTC 0.6B Japanese (int8))
[SKIP] 以下の Qwen モデルはスキップします:
  - qwen-2.5-1.5b-int4 (Qwen 2.5 1.5B ONNX (int4))
```

### 例2: 音声認識のみ（Sherpa-ONNX）

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en",
    "whisper-tiny"
  ]
}
```

### 例3: 全モデル有効化

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en",
    "zipformer-ja-reazonspeech",
    "whisper-tiny",
    "nemo-parakeet-cja",
    "qwen-2.5-0.5b",
    "qwen-2.5-1.5b-int4"
  ]
}
```

または、コマンドラインから：

```bash
ModelPrepTool --model all
```

### 例4: 特定のモデルのみ準備

```bash
# SenseVoice だけ準備
ModelPrepTool --model sense-voice-ja-zh-en

# Qwen 0.5B だけ準備
ModelPrepTool --model qwen-2.5-0.5b
```

この場合、設定ファイルは使用されません。

## コマンドラインオプション

### デフォルト設定ファイルを使用

```bash
ModelPrepTool
```

この場合：
1. `model-prep-config.json` を探します
2. 見つからない場合は自動作成します
3. 設定に基づいてモデルをフィルタリングします

### カスタム設定ファイルを指定

```bash
ModelPrepTool --config path/to/custom-config.json
```

### 個別モデルを準備（設定ファイル無視）

```bash
ModelPrepTool --model sense-voice-ja-zh-en
```

### 全モデル準備（設定ファイル無視）

```bash
ModelPrepTool --model all
```

### ヘルプを表示

```bash
ModelPrepTool --help
```

## トラブルシューティング

### Q: スキップされたモデルを見て、後で追加したい場合

**A:** `model-prep-config.json` を編集して、モデル ID を `enabledModels` に追加してください。

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en",
    "zipformer-ja-reazonspeech",  ← 追加
    "qwen-2.5-0.5b"
  ]
}
```

### Q: デフォルト設定をリセットしたい場合

**A:** `model-prep-config.json` を削除すれば、次回実行時にデフォルト設定が自動生成されます。

```bash
rm model-prep-config.json
ModelPrepTool
```

### Q: 設定ファイルが見つからないエラーが出た

**A:** `model-prep-config.json` がビルド出力ディレクトリにあるか確認してください：

```bash
ls -la bin/Debug/net10.0/model-prep-config.json
```

なければ、ツールを実行すれば自動生成されます。

## ベストプラクティス

### 開発環境での推奨設定

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en",
    "qwen-2.5-0.5b"
  ]
}
```

- 容量：約 241 MB（SenseVoice） + 3 GB（Qwen 0.5B）
- バランス：認識と推論機能を両方備える

### ストレージが限られている場合

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en"
  ]
}
```

- 容量：約 241 MB
- 用途：音声認識のみ

### 高精度が必要な場合

```json
{
  "enabledModels": [
    "sense-voice-ja-zh-en",
    "nemo-parakeet-cja",
    "qwen-2.5-1.5b-int4"
  ]
}
```

- 容量：約 250 MB + 625 MB + 9 GB
- メリット：精度の高いモデル群

## 設定ファイルのバージョン管理

Git を使っている場合、`model-prep-config.json` をプロジェクトに含めることで、チーム全体で同じモデルセットを使用できます。

```bash
# リポジトリに追加
git add src_dotnet/ModelPrepTool/model-prep-config.json
git commit -m "Add model-prep-config with recommended models"
```

これにより、誰が `ModelPrepTool` を実行しても、同じモデルセットがダウンロードされます。
