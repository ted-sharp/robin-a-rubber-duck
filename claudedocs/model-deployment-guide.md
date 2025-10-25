# Model Deployment Guide

Manual deployment script for Sherpa-ONNX models to Android devices.

## Overview

`scripts/deploy-models-to-device.ps1` automates downloading Sherpa-ONNX models from GitHub releases and deploying them to connected Android devices via adb.

## Prerequisites

- **Windows PowerShell** (5.1 or later)
- **Android SDK Platform Tools** (adb)
- **tar** utility (included in Windows 10+)
- **USB debugging enabled** on Android device
- **~500MB free space** on PC (for downloads)
- **~300MB free space** on device (per model)

## Usage

### Interactive Mode (Recommended)

```powershell
# Run script with menu selection
.\scripts\deploy-models-to-device.ps1
```

Menu options:
- `[1-4]`: Deploy specific model
- `[A]`: Deploy all models
- `[Q]`: Quit

### Command-Line Mode

```powershell
# Deploy specific model
.\scripts\deploy-models-to-device.ps1 -ModelType "sense-voice-ja-zh-en"

# Deploy without re-downloading (use cached files)
.\scripts\deploy-models-to-device.ps1 -ModelType "zipformer-ja-reazonspeech" -SkipDownload

# Clean all models from device
.\scripts\deploy-models-to-device.ps1 -CleanDevice

# Deploy to custom device path
.\scripts\deploy-models-to-device.ps1 -DeviceStorage "/sdcard/MyModels"
```

## Available Models

### 1. SenseVoice Multilingual (int8)
- **Model ID**: `sense-voice-ja-zh-en`
- **Size**: ~227MB
- **Languages**: Japanese, Chinese, English, Korean, Cantonese
- **Best for**: Multilingual applications, general purpose
- **Current default in Robin app**

### 2. Zipformer Japanese ReazonSpeech
- **Model ID**: `zipformer-ja-reazonspeech`
- **Size**: ~140MB
- **Languages**: Japanese only
- **Best for**: Japanese-focused applications, better accuracy for Japanese

### 3. Zipformer English 2023
- **Model ID**: `zipformer-en-2023`
- **Size**: ~66MB
- **Languages**: English only
- **Best for**: English-only applications, smaller size

### 4. Whisper Tiny English
- **Model ID**: `whisper-tiny-en`
- **Size**: ~39MB
- **Languages**: English only
- **Best for**: Resource-constrained devices, fastest inference

## Deployment Process

The script performs these steps:

### Step 1: Check adb Connection
- Verifies adb is installed
- Confirms device is connected
- Validates USB debugging enabled

### Step 2: Model Selection
- Interactive menu or command-line parameter
- Shows model details (size, languages)
- Allows single or batch deployment

### Step 3: Download Model
- Downloads from GitHub releases
- Extracts tar.bz2 archive
- Caches in `models-temp/` directory
- Skips if already downloaded

### Step 4: Deploy to Device
- Creates `/sdcard/Download/sherpa-models/` directory
- Pushes model files via adb
- Preserves directory structure

### Step 5: Verification
- Lists deployed files on device
- Shows file sizes
- Confirms deployment success

## Device Storage Structure

After deployment:

```
/sdcard/Download/sherpa-models/
├── sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/
│   ├── model.int8.onnx          (~227MB)
│   └── tokens.txt                (~400KB)
├── sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01/
│   ├── encoder-epoch-99-avg-1.int8.onnx  (~47MB)
│   ├── decoder-epoch-99-avg-1.int8.onnx  (~1MB)
│   ├── joiner-epoch-99-avg-1.int8.onnx   (~11MB)
│   └── tokens.txt                         (~400KB)
└── [other models]/
```

## Using Deployed Models in Robin

### Current Approach (Manual Setup)

1. **Deploy model** using this script
2. **Manually copy** from device storage to `Resources/raw/` in project
3. **Add to csproj** with `AndroidAsset` build action
4. **Rebuild app** with new model included

**Example workflow:**

```bash
# 1. Deploy to device
.\scripts\deploy-models-to-device.ps1 -ModelType "zipformer-ja-reazonspeech"

# 2. Pull from device to project
adb pull /sdcard/Download/sherpa-models/sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01 src_dotnet/Robin/Resources/raw/

# 3. Add to Robin.csproj (manually edit)
# <AndroidAsset Include="Resources\raw\sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01\*.onnx">
#   <Link>assets\sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01\%(Filename)%(Extension)</Link>
# </AndroidAsset>

# 4. Rebuild app
dotnet build src_dotnet/Robin/Robin.csproj
```

### Future Enhancement (Runtime Download)

Planned feature in `ModelDownloadService.cs`:

1. App detects missing model
2. Shows download dialog with model selection
3. Downloads directly to device storage
4. Copies to app's private storage
5. Initializes recognizer with new model

**Benefits:**
- No manual file copying
- User can switch models without rebuilding
- Smaller APK size (models not bundled)
- Dynamic model management

See `claudedocs/runtime-download-implementation.md` for implementation plan.

## Troubleshooting

### "adb not found"

**Cause**: Android SDK Platform Tools not in PATH

**Solution**:
```powershell
# Option 1: Add to PATH environment variable
$env:Path += ";C:\path\to\platform-tools"

# Option 2: Use full path
& "C:\Users\YourName\AppData\Local\Android\Sdk\platform-tools\adb.exe" devices
```

### "No Android devices connected"

**Cause**: Device not connected or USB debugging disabled

**Solution**:
1. Connect device via USB
2. Enable Developer Options (tap Build Number 7 times)
3. Enable USB Debugging in Developer Options
4. Accept "Allow USB debugging" prompt on device
5. Verify: `adb devices` shows device

### "Extraction failed"

**Cause**: tar utility not available

**Solution**:
- Windows 10+: tar is built-in, check `where tar`
- Older Windows: Install Git for Windows (includes tar)
- Alternative: Use 7-Zip to extract `.tar.bz2` manually

### "adb push failed"

**Cause**: Permission denied or storage full

**Solution**:
- Check device storage: `adb shell df -h /sdcard`
- Try different path: `-DeviceStorage "/sdcard/Documents/models"`
- Grant storage permission if prompted on device

### Model files missing after deployment

**Cause**: Path mismatch or cleanup

**Solution**:
```bash
# Verify files exist
adb shell "ls -lR /sdcard/Download/sherpa-models/"

# Check available storage
adb shell "du -sh /sdcard/Download/sherpa-models/*"
```

## Cleanup

### Remove all models from device:
```powershell
.\scripts\deploy-models-to-device.ps1 -CleanDevice
```

### Remove cached downloads on PC:
```powershell
Remove-Item -Recurse -Force .\models-temp\
```

## Performance Considerations

### Download Times
- Fast connection (50 Mbps): 30-60 seconds per model
- Slow connection (5 Mbps): 5-10 minutes per model
- Use `-SkipDownload` flag for cached models

### Transfer Times
- USB 3.0: 10-30 seconds per model
- USB 2.0: 30-90 seconds per model
- Wireless adb: 2-5 minutes per model (not recommended)

### Model Size Impact

| Model | Size | APK Impact | Runtime Memory |
|-------|------|------------|----------------|
| Whisper Tiny | 39MB | +39MB | ~150MB |
| Zipformer EN | 66MB | +66MB | ~200MB |
| Zipformer JA | 140MB | +140MB | ~250MB |
| SenseVoice | 227MB | +227MB | ~300MB |

**Recommendation**: Only bundle one model in APK, allow users to download others at runtime.

## Advanced Usage

### Batch Deployment Script

```powershell
# Deploy all models to multiple devices
adb devices | Select-String "device$" | ForEach-Object {
    $deviceId = $_.ToString().Split()[0]
    Write-Host "Deploying to device: $deviceId"
    $env:ANDROID_SERIAL = $deviceId
    .\scripts\deploy-models-to-device.ps1 -ModelType "sense-voice-ja-zh-en" -SkipDownload
}
```

### Custom Model URLs

Edit `$Models` hashtable in script to add custom models:

```powershell
$Models = @{
    "custom-model" = @{
        Name = "My Custom Model"
        Url = "https://example.com/model.tar.bz2"
        Archive = "model.tar.bz2"
        Folder = "model-folder"
        Size = "~100MB"
        Languages = "Custom"
    }
}
```

## Integration with Build Process

### Automated Model Deployment (Future)

Add to `Robin.csproj` as pre-build target:

```xml
<Target Name="DeployModels" BeforeTargets="Build" Condition="'$(DeployModels)' == 'true'">
  <Exec Command="powershell -File scripts\deploy-models-to-device.ps1 -ModelType $(ModelType) -SkipDownload" />
</Target>
```

Usage:
```bash
dotnet build -p:DeployModels=true -p:ModelType=sense-voice-ja-zh-en
```

## See Also

- `claudedocs/sherpa-onnx-setup.md` - Initial Sherpa-ONNX setup
- `claudedocs/runtime-download-implementation.md` - Runtime download feature plan
- `claudedocs/dotnet-android-assets-guide.md` - Asset packaging details
- [Sherpa-ONNX Models](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html) - Official model list
