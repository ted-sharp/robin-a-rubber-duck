# ModelPrepTool

Command-line tool for downloading and preparing Sherpa-ONNX speech recognition models on PC.

## Overview

ModelPrepTool is a .NET 10.0 console application that downloads Japanese-compatible Sherpa-ONNX models from GitHub releases and prepares them for use with the Robin Android app.

## Purpose

This tool solves the problem of large model downloads on mobile devices:
- ✅ Download models on PC (faster, unlimited data)
- ✅ Prepare and verify models before deployment
- ✅ Transfer to Android device via USB/adb
- ✅ Cache downloads for reuse across devices

## Features

- **Model Management**: List, download, and verify Sherpa-ONNX models
- **Progress Reporting**: Real-time download progress with speed indicators
- **Smart Caching**: Skip already-downloaded and extracted models
- **File Verification**: Ensure all required model files are present
- **Cleanup**: Remove archive files while preserving extracted models
- **Shared Model Definitions**: Uses Robin.Core for centralized model metadata

## Installation

### Prerequisites

- .NET 10.0 SDK
- `tar` utility (included in Windows 10+, macOS, Linux)
- ~1 GB free disk space for model storage

### Build

```bash
cd src_dotnet/ModelPrepTool
dotnet build
```

## Usage

### List Available Models

```bash
dotnet run -- --list
# or
dotnet run -- -l
```

Output:
```
=== Japanese-Compatible Sherpa-ONNX Models ===

[sense-voice-ja-zh-en]
  Name: SenseVoice Multilingual
  Size: 238.0 MB
  Languages: Japanese, Chinese, English, Korean, Cantonese
  Description: Multilingual model with Japanese support, general purpose
  Status: Not prepared

[zipformer-ja-reazonspeech]
  Name: Zipformer Japanese ReazonSpeech
  Size: 680.0 MB
  Languages: Japanese
  Description: Japanese-only model with high accuracy
  Status: Ready
```

### Download Specific Model

```bash
# SenseVoice Multilingual
dotnet run -- --model sense-voice-ja-zh-en

# Zipformer Japanese
dotnet run -- --model zipformer-ja-reazonspeech

# Short form
dotnet run -- -m sense-voice-ja-zh-en
```

### Download All Models

```bash
dotnet run -- --model all
# or just
dotnet run
```

### Custom Output Directory

```bash
dotnet run -- --output /path/to/models
# or
dotnet run -- -o D:\MyModels
```

### Clean Cache

Remove `.tar.bz2` archive files (keep extracted models):

```bash
dotnet run -- --clean
# or
dotnet run -- -c
```

### Help

```bash
dotnet run -- --help
# or
dotnet run -- -h
```

## Output

Models are downloaded and extracted to `models-prepared/` by default:

```
models-prepared/
├── sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/
│   ├── model.int8.onnx          (227 MB)
│   └── tokens.txt                (400 KB)
├── sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01/
│   ├── encoder-epoch-99-avg-1.int8.onnx  (47 MB)
│   ├── decoder-epoch-99-avg-1.int8.onnx  (1 MB)
│   ├── joiner-epoch-99-avg-1.int8.onnx   (11 MB)
│   └── tokens.txt                         (45 KB)
└── [archives]
    ├── sherpa-onnx-sense-voice-*.tar.bz2
    └── sherpa-onnx-zipformer-ja-*.tar.bz2
```

## Workflow

### 1. Prepare Models on PC

```bash
# Download Japanese models
dotnet run -- --model all
```

### 2. Transfer to Android Device

**Important**: Models are NOT bundled in APK. They must be transferred to device storage.

Choose one of two methods:

#### Option 1: USB File Transfer (Recommended for most users)

1. Connect Android device via USB
2. Open device in File Explorer (Windows) or Finder (macOS)
3. Navigate to `Internal Storage/Download/` on device
4. Create `sherpa-models/` folder if it doesn't exist
5. Copy prepared model folder from `models-prepared/` to device
6. In Robin app: **Settings → Model Path → Browse** and select the model folder

**Pros**:
- Simple, no command-line tools needed
- Visual confirmation of file transfer
- Works on all platforms

**Cons**:
- Manual drag-and-drop process
- Slower for multiple devices

#### Option 2: adb Push (Recommended for developers)

```bash
# Transfer model to device
adb push models-prepared/sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01 /sdcard/Download/sherpa-models/

# Verify transfer
adb shell "ls -lh /sdcard/Download/sherpa-models/"

# In Robin app: Settings → Model Path → /sdcard/Download/sherpa-models/[model-folder]
```

**Pros**:
- Fast, scriptable
- Ideal for CI/CD and automation
- Batch deployment to multiple devices

**Cons**:
- Requires adb setup
- Command-line knowledge needed

#### Future: App Runtime Download

The Robin app will support downloading models directly from the device in a future update. This PC preparation tool ensures models are ready for quick transfer when needed.

## Examples

### Complete Workflow

```bash
# 1. Check available models
dotnet run -- --list

# 2. Download Zipformer Japanese model
dotnet run -- --model zipformer-ja-reazonspeech

# 3. Transfer to device
adb push models-prepared/sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01 /sdcard/Download/sherpa-models/

# 4. Clean up archives to save space
dotnet run -- --clean
```

### Development Cycle

```bash
# Download once
dotnet run -- --model sense-voice-ja-zh-en

# Deploy to multiple test devices
for device in $(adb devices | grep device | cut -f1); do
    ANDROID_SERIAL=$device adb push models-prepared/sherpa-onnx-sense-voice-* /sdcard/Download/sherpa-models/
done
```

## Performance

### Download Times (50 Mbps connection)

- **SenseVoice** (227 MB compressed): ~40 seconds
- **Zipformer JA** (140 MB compressed): ~25 seconds

### Transfer Times (USB 3.0)

- **SenseVoice** (238 MB extracted): ~10 seconds
- **Zipformer JA** (680 MB extracted): ~30 seconds

## Troubleshooting

### "tar: command not found"

**Solution**: Ensure `tar` is in PATH. Windows 10+ includes tar by default.

```bash
# Check tar availability
where tar
# or
which tar
```

### Download fails with timeout

**Solution**: Increase timeout or check network connection. GitHub releases may be slow depending on location.

### "Model verification failed"

**Solution**:
1. Delete incomplete model folder
2. Delete archive file
3. Re-run download

```bash
rm -rf models-prepared/sherpa-onnx-*
dotnet run -- --model [model-id]
```

### Disk space issues

**Solution**: Clean archives after extraction

```bash
dotnet run -- --clean
```

## Architecture

ModelPrepTool uses **Robin.Core** shared library for:
- `SherpaModelDefinition` - Model metadata (URLs, files, sizes)
- `ModelDownloader` - Download and extraction logic
- `ModelVerifier` - File verification

This ensures model definitions are consistent across PC tools and mobile apps.

## Future Enhancements

- [ ] Parallel downloads for multiple models
- [ ] SHA256 checksum verification
- [ ] Direct adb push integration (`--deploy` flag)
- [ ] Model compression optimization
- [ ] Differential updates for model versions

## See Also

- **Robin.Core** - Shared library with model definitions
- **Robin** - Android MAUI voice chat application
- [Sherpa-ONNX Models](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html) - Official model documentation

## License

Part of the Robin voice chat application project.
