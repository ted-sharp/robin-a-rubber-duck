# Robin - Voice Chat Application

Android voice chat application with offline speech recognition capabilities. Discord-inspired UI with voice input that converts speech to text, sends to OpenAI API, and displays AI responses.

## Features

- 🎤 **Multi-Provider Speech Recognition**
  - **On-Device**: Android standard SpeechRecognizer, Sherpa-ONNX offline (4 models)
  - **Cloud API**: Azure Speech-to-Text
  - **LAN Server**: Faster Whisper (self-hosted)
  - Runtime switching via drawer menu

- 💬 **Multi-Provider AI Chat**
  - **Cloud**: OpenAI GPT, Azure OpenAI, Anthropic Claude
  - **Local**: LM Studio (self-hosted LLM on local network)
  - Multiple provider profiles (LM Studio 1/2)
  - Runtime provider switching

- ⚙️ **Flexible Configuration**
  - JSON file import for API credentials
  - Settings persistence via SharedPreferences
  - Sample configurations in `config-samples/`
  - Drawer-based settings UI

- 🌐 **Multi-Language Support**
  - Japanese (optimized)
  - English, Chinese, Korean, Cantonese (SenseVoice model)
  - Language-specific models available

- 📱 **Modern Android UI**
  - Drawer navigation with settings
  - RecyclerView-based chat interface
  - Material Design components
  - Real-time provider status display

## Project Structure

```
robin-a-rubber-duck/
├── src_dotnet/
│   ├── Robin/                    # Main MAUI Android application
│   ├── Robin.Core/               # Shared library (models, services)
│   └── ModelPrepTool/            # PC-side model preparation tool
├── config-samples/               # External API configuration samples
│   ├── llm-*.json                # LLM provider configs (OpenAI, Azure, Claude, LM Studio)
│   ├── asr-*.json                # ASR provider configs (Azure STT, Faster Whisper)
│   └── README.md                 # Configuration guide
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

#### 2. Configure External APIs (Optional)

For LLM or cloud ASR providers, prepare configuration files:

```bash
# Copy sample configuration
cd config-samples/
cp llm-openai-sample.json llm-openai.local.json

# Edit with your API key
# nano llm-openai.local.json (or use your preferred editor)
```

**Available Configurations:**
- **LLM**: OpenAI, Azure OpenAI, Claude, LM Studio
- **ASR**: Azure Speech-to-Text, Faster Whisper

See `config-samples/README.md` for detailed configuration guide.

#### 3. Build with Models

```bash
# Build the application (models are auto-included as AndroidAssets if present)
dotnet build src_dotnet/Robin/Robin.csproj

# Or build and install directly
dotnet build -t:Install src_dotnet/Robin/Robin.csproj
```

The build system automatically detects models in `models-prepared/` and includes them in the APK.

#### 4. Import Configuration (if using external APIs)

```bash
# Transfer config to device
adb push llm-openai.local.json /sdcard/Download/

# Or use USB file transfer to copy to Download folder
```

In the app:
1. Open drawer menu (swipe from left)
2. Select "LLMモデル選択" or "音声認識モデル管理"
3. Choose provider and tap "設定ファイルから読み込み"
4. Select your config file from Downloads

#### 5. View Logs

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

## Available Providers

### Speech Recognition (ASR)

**On-Device Recognition:**
1. **Android Standard SpeechRecognizer**
   - Online, requires network
   - System-provided recognition
   - Quick setup, no additional configuration

2. **Sherpa-ONNX Offline Models**
   - **SenseVoice Multilingual (int8)**: ~238 MB, Japanese/Chinese/English/Korean/Cantonese
   - **Zipformer Japanese ReazonSpeech**: ~680 MB, High accuracy Japanese
   - **Nemo CTC Japanese**: ~625 MB, Alternative Japanese model
   - **Streaming Zipformer Multilingual**: ~247 MB, 8 languages

**Cloud/Network Recognition:**
3. **Azure Speech-to-Text**
   - Microsoft Azure cloud service
   - High accuracy, multiple languages
   - Requires API subscription

4. **Faster Whisper (LAN Server)**
   - Self-hosted on local network
   - OpenAI Whisper model variants
   - No cloud dependency, local processing

### AI Chat (LLM)

**Cloud Providers:**
1. **OpenAI**
   - Models: GPT-4o, GPT-4o-mini, GPT-3.5-turbo
   - API key required

2. **Azure OpenAI**
   - Microsoft Azure-hosted OpenAI models
   - Enterprise-grade deployment

3. **Anthropic Claude**
   - Models: Claude 3.5 Sonnet, Claude 3.5 Haiku
   - API key required

**Local Providers:**
4. **LM Studio**
   - Self-hosted LLM on local network
   - Run open-source models (Qwen, Llama, etc.)
   - Supports 2 different server profiles
   - No API key needed

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

**Provider Selection:**
- **ASR**: Select via drawer menu → "音声認識モデル管理"
  - Android standard, Sherpa-ONNX models, Azure STT, Faster Whisper
  - Runtime switching supported

- **LLM**: Select via drawer menu → "LLMモデル選択"
  - OpenAI, Azure OpenAI, Claude, LM Studio (2 profiles)
  - Runtime switching supported

**Configuration Files:**
- Place JSON config files in `/sdcard/Download/`
- Import via drawer menu settings
- See `config-samples/` for templates

**Permissions:**
- `RECORD_AUDIO` - Required, requested at runtime
- `INTERNET` - Required for cloud API providers

## Documentation

- **Architecture**: `claudedocs/architecture-design.md`
- **Sherpa-ONNX Integration**: `claudedocs/sherpa-onnx-integration.md`
- **Setup Guide**: `claudedocs/sherpa-onnx-setup.md`
- **Model Preparation**: `claudedocs/pc-model-preparation-guide.md`
- **Implementation Status**: `claudedocs/implementation-status.md`
- **Asset Packaging**: `claudedocs/dotnet-android-assets-guide.md`
- **API Configuration**: `config-samples/README.md`

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

- [ ] Runtime model download UI (via Robin.Core)
- [ ] True streaming recognition (OnlineRecognizer instead of chunks)
- [ ] Voice Activity Detection (VAD) integration
- [ ] Encrypted SharedPreferences for API keys
- [ ] Text-to-speech (TTS) for AI responses
- [ ] Offline mode with queued API calls
- [ ] Conversation export/import
- [ ] Custom system prompts UI

## Contributing

This is a personal project for learning and experimentation. Suggestions and feedback welcome!

## License

Personal project - no formal license at this time.

## Resources

- [Sherpa-ONNX Official Documentation](https://k2-fsa.github.io/sherpa/onnx/)
- [Sherpa-ONNX Pretrained Models](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html)
- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [OpenAI API Documentation](https://platform.openai.com/docs/)
