# Robin - Voice Chat Application

Android voice chat application with offline speech recognition capabilities. Discord-inspired UI with voice input that converts speech to text, sends to OpenAI API, and displays AI responses.

## Features

- 🎤 **Dual Speech Recognition Engines**
  - Android standard SpeechRecognizer (online)
  - Sherpa-ONNX offline recognition (no network required)

- 🌐 **Japanese Language Focus**
  - Optimized for Japanese speech recognition
  - Multiple Japanese-compatible models available

- 💬 **AI Chat Integration**
  - OpenAI API integration for conversational responses
  - Message history management

- 📱 **Modern Android UI**
  - Drawer navigation
  - RecyclerView-based chat interface
  - Material Design components

## Project Structure

```
robin-a-rubber-duck/
├── src_dotnet/
│   ├── Robin/                    # Main MAUI Android application
│   ├── Robin.Core/               # Shared library (models, services)
│   └── ModelPrepTool/            # PC-side model preparation tool
├── scripts/
│   └── prepare-ja-models.ps1     # Legacy PowerShell script
├── models-prepared/              # Downloaded Sherpa-ONNX models
└── claudedocs/                   # Development documentation
```

## Projects

### Robin (MAUI Android App)

Main voice chat application for Android devices.

**Technology Stack:**
- .NET 10.0 Android (net10.0-android)
- Minimum Android API 24 (Android 7.0)
- C# 12 with nullable reference types
- Native Android Views (DrawerLayout, RecyclerView)
- Sherpa-ONNX 1.12.15 for offline speech recognition

**Key Components:**
- `MainActivity.cs` - Main entry point, UI management
- `Services/VoiceInputService.cs` - Android standard speech recognition
- `Services/SherpaRealtimeService.cs` - Sherpa-ONNX offline recognition
- `Services/OpenAIService.cs` - OpenAI API integration
- `Services/ConversationService.cs` - Message history management

**Build:**
```bash
dotnet build src_dotnet/Robin/Robin.csproj
```

**Install to device:**
```bash
dotnet build -t:Install src_dotnet/Robin/Robin.csproj
```

See: [Robin Project Documentation](src_dotnet/Robin/README.md) *(to be created)*

### Robin.Core (Shared Library)

Common models and services for Sherpa-ONNX model management.

**Purpose:**
- Centralized model definitions (URLs, metadata)
- Shared download and verification logic
- Used by ModelPrepTool and Robin app

**Components:**
- `Models/SherpaModelDefinition.cs` - Model metadata
- `Services/ModelDownloader.cs` - Download and extraction
- `Services/ModelVerifier.cs` - File verification

**Build:**
```bash
dotnet build src_dotnet/Robin.Core/Robin.Core.csproj
```

See: [Robin.Core README](src_dotnet/Robin.Core/README.md)

### ModelPrepTool (Console Application)

PC-side tool for downloading and preparing Sherpa-ONNX models.

**Purpose:**
- Download models on PC (faster, unlimited data)
- Prepare models for Android deployment
- Verify model integrity

**Usage:**
```bash
# List available models
dotnet run --project src_dotnet/ModelPrepTool -- --list

# Download specific model
dotnet run --project src_dotnet/ModelPrepTool -- --model zipformer-ja-reazonspeech

# Download all models
dotnet run --project src_dotnet/ModelPrepTool -- --model all
```

See: [ModelPrepTool README](src_dotnet/ModelPrepTool/README.md)

## Quick Start

### Development Setup (Model Bundled in APK)

**Note**: Models are prepared on PC and bundled in APK for development. This approach is suitable for development and testing.

#### 1. Download and Prepare Models on PC

```bash
# List available models
dotnet run --project src_dotnet/ModelPrepTool -- --list

# Download Japanese model (Zipformer is recommended for high accuracy)
dotnet run --project src_dotnet/ModelPrepTool -- --model zipformer-ja-reazonspeech

# Or download all models
dotnet run --project src_dotnet/ModelPrepTool -- --model all
```

This creates `models-prepared/` directory with extracted model files.

#### 2. Build with Models

```bash
# Build the application (models are auto-included as AndroidAssets if present)
dotnet build src_dotnet/Robin/Robin.csproj

# Or build and install directly
dotnet build -t:Install src_dotnet/Robin/Robin.csproj
```

The build system automatically detects models in `models-prepared/` and includes them in the APK.

#### 3. View Logs

```bash
# Monitor app logs
adb logcat | grep Robin
```

### Production Deployment (Models on Device Storage)

For production builds where bundling large models isn't desired, transfer models directly to device storage:

```bash
# Connect device and transfer model
adb push models-prepared/sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01 /sdcard/Download/sherpa-models/

# Verify transfer
adb shell "ls -lh /sdcard/Download/sherpa-models/"
```

In the future, the app will support runtime model selection from device storage.

## Available Speech Recognition Models

### SenseVoice Multilingual (int8)
- **Size**: ~238 MB (extracted)
- **Languages**: Japanese, Chinese, English, Korean, Cantonese
- **Use Case**: General purpose, multilingual support

### Zipformer Japanese ReazonSpeech
- **Size**: ~680 MB (extracted)
- **Languages**: Japanese only
- **Use Case**: High accuracy Japanese-specific recognition

## Development

### Prerequisites

- .NET 10.0 SDK
- Android SDK (API 24+)
- Visual Studio 2022 or VS Code
- adb (Android Debug Bridge)

### Environment Setup

**Platform**: Windows with MSYS2 bash
- Unix-style paths in bash: `/c/git/...`
- Native commands work: `dotnet`, `adb`, `powershell`

### Building and Testing

```bash
# Clean build
dotnet clean src_dotnet/Robin/Robin.csproj
dotnet build src_dotnet/Robin/Robin.csproj

# Release build
dotnet build -c Release src_dotnet/Robin/Robin.csproj

# List connected devices
adb devices

# View logs
adb logcat | grep Robin
```

### Project Configuration

**Engine Selection:**
Toggle between Android standard and Sherpa-ONNX in `MainActivity.cs`:
```csharp
private bool _useSherpaOnnx = true; // true: Sherpa-ONNX, false: Android標準
```

**Permissions:**
- `RECORD_AUDIO` - Required, requested at runtime
- `INTERNET` - Required for OpenAI API

## Documentation

- **Architecture**: `claudedocs/architecture-design.md`
- **Sherpa-ONNX Integration**: `claudedocs/sherpa-onnx-integration.md`
- **Setup Guide**: `claudedocs/sherpa-onnx-setup.md`
- **Model Preparation**: `claudedocs/pc-model-preparation-guide.md`
- **Implementation Status**: `claudedocs/implementation-status.md`
- **Asset Packaging**: `claudedocs/dotnet-android-assets-guide.md`

## Key Technologies

### Speech Recognition
- **Sherpa-ONNX 1.12.15** - Offline speech recognition via Java interop
- **Android SpeechRecognizer** - System speech recognition API
- **ONNX Runtime** - Neural network inference engine

### AI Integration
- **OpenAI API** - Chat completion for conversational responses
- **JSON serialization** - System.Text.Json for API communication

### Mobile Development
- **.NET MAUI** - Cross-platform UI framework (Android-focused)
- **Native Android Views** - DrawerLayout, RecyclerView, FloatingActionButton
- **Material Design** - Android Material Components

## Architecture Overview

### Dual Recognition Strategy

```
User taps mic
    ↓
┌───────────────┐
│  MainActivity │
└───────┬───────┘
        ├─→ VoiceInputService (Android Standard) ─→ Online recognition
        └─→ SherpaRealtimeService (Sherpa-ONNX) ─→ Offline recognition
                ↓
        RecognitionResult event
                ↓
        ConversationService.AddUserMessage()
                ↓
        OpenAIService.SendMessageAsync()
                ↓
        ConversationService.AddAIMessage()
                ↓
        RecyclerView updated
```

### Model Management (PC → Device)

```
PC:
┌──────────────┐
│ModelPrepTool │ ─→ Download from GitHub
└──────┬───────┘
       ↓
   models-prepared/
       ↓
   USB/adb transfer
       ↓
Device:
/sdcard/Download/sherpa-models/
       ↓
Robin app reads models
```

## Performance Considerations

### APK Size
- Base app: ~5-10 MB
- Sherpa-ONNX AAR: +37 MB
- SenseVoice model (bundled): +227 MB
- Zipformer model (bundled): +680 MB

**Recommendation**: Use ModelPrepTool + device transfer instead of bundling models in APK.

### Runtime Memory
- Model loading: ~300 MB
- Active recognition: +100 MB
- Total: ~400 MB during use

### CPU Usage
- Continuous recognition: 20-40% of one core
- Chunk processing reduces sustained load

## Testing

### Manual Testing Checklist
- [ ] RECORD_AUDIO permission request
- [ ] Android standard recognition
- [ ] Sherpa-ONNX recognition
- [ ] Continuous mode auto-restart
- [ ] OpenAI API integration
- [ ] Drawer navigation
- [ ] Device rotation handling

### Test Scenarios
- Voice input in quiet environment
- Voice input with background noise
- Long conversation history
- Rapid mic button tapping
- App backgrounding during recognition

## Troubleshooting

### Build Errors

**"Could not find sherpa-onnx-1.12.15.aar"**
- Ensure file exists in `src_dotnet/Robin/libs/`

**"Method 'finalize' not found"**
- Verify `Transforms/Metadata.xml` excludes finalize methods

### Runtime Issues

**"Sherpa-ONNX 初期化失敗"**
- Check model files exist in assets
- Verify paths in InitializeAsync

**Recognition not working**
- Verify microphone permission granted
- Check logcat for errors

**OpenAI errors**
- Validate API key
- Check network connectivity

## Future Enhancements

- [ ] Settings UI for engine selection
- [ ] Runtime model download (via Robin.Core)
- [ ] True streaming recognition (OnlineRecognizer)
- [ ] Voice Activity Detection (VAD)
- [ ] Multiple model support with selection UI
- [ ] Secure API key storage
- [ ] Text-to-speech for AI responses
- [ ] Offline mode with queued API calls

## Contributing

This is a personal project for learning and experimentation. Suggestions and feedback welcome!

## License

Personal project - no formal license at this time.

## Resources

- [Sherpa-ONNX Official Documentation](https://k2-fsa.github.io/sherpa/onnx/)
- [Sherpa-ONNX Pretrained Models](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html)
- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [OpenAI API Documentation](https://platform.openai.com/docs/)
