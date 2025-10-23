# Sherpa-ONNX セットアップ手順

## 必要なファイルのダウンロード

### 1. Sherpa-ONNX AARライブラリ

最新版（v1.12.15）をダウンロード：

```powershell
# libsディレクトリに移動
cd C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin\libs

# AARファイルをダウンロード
Invoke-WebRequest -Uri "https://github.com/k2-fsa/sherpa-onnx/releases/download/v1.12.15/sherpa-onnx-1.12.15.aar" -OutFile "sherpa-onnx-1.12.15.aar"
```

### 2. 日本語モデルのダウンロード

SenseVoiceマルチ言語モデル（日本語対応）：

```powershell
# Rawディレクトリに移動（Androidのassetsとして配置）
cd C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin\Resources\raw

# モデルをダウンロード
Invoke-WebRequest -Uri "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17.tar.bz2" -OutFile "sherpa-onnx-sense-voice.tar.bz2"

# 解凍（7-zipまたはtar）
tar -xjf sherpa-onnx-sense-voice.tar.bz2
```

### 3. ファイル構成

ダウンロード後のディレクトリ構成：

```
Robin/
├── libs/
│   └── sherpa-onnx-1.12.15.aar
├── Resources/
│   └── raw/
│       └── sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17/
│           ├── model.onnx
│           ├── tokens.txt
│           └── ...
```

## 次のステップ

1. Robin.csprojにライブラリ参照を追加
2. SherpaRealtimeServiceを実装
3. MainActivityに統合
