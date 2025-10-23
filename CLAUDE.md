# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Robin** - Android voice chat application with offline speech recognition capabilities. Discord-inspired UI with voice input that converts speech to text, sends to OpenAI API, and displays AI responses. Features dual speech recognition engines (Android standard + Sherpa-ONNX offline).

**Technology Stack:**
- .NET 10.0 Android (net10.0-android)
- Minimum Android API 24 (Android 7.0)
- C# 12 with nullable reference types
- Native Android Views (DrawerLayout, RecyclerView)
- Sherpa-ONNX 1.12.15 for offline speech recognition

## Development Environment

**Platform**: Windows with MSYS2 bash
**Shell**: bash (MSYS2 environment)
**Path Style**: Unix-style paths (`/c/git/...`) in bash commands
**Native Commands**: Windows native commands (dotnet, powershell, etc.) work in MSYS2

## Build and Development Commands

### Build Commands
```bash
# Clean build
dotnet clean "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin\Robin.csproj"

# Build project
dotnet build "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin\Robin.csproj"

# Install to connected device
dotnet build -t:Install "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin\Robin.csproj"

# Release build
dotnet build -c Release "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin\Robin.csproj"
```

### Device Management
```bash
# List connected Android devices
adb devices

# View logcat for debugging
adb logcat | grep Robin
```

### Project Structure
```
robin-a-rubber-duck/
├── src_dotnet/Robin/
│   ├── MainActivity.cs              # Main entry point, DrawerLayout management
│   ├── Services/
│   │   ├── VoiceInputService.cs    # Android standard SpeechRecognizer
│   │   ├── SherpaRealtimeService.cs # Sherpa-ONNX offline recognition
│   │   ├── OpenAIService.cs         # OpenAI API communication
│   │   └── ConversationService.cs   # Message history management
│   ├── Adapters/
│   │   └── MessageAdapter.cs        # RecyclerView adapter for chat
│   ├── Models/
│   │   ├── Message.cs               # Chat message model
│   │   └── OpenAIModels.cs          # OpenAI API DTOs
│   ├── Views/
│   │   └── SwipeHintView.cs         # UI hint component
│   ├── libs/
│   │   └── sherpa-onnx-1.12.15.aar  # Sherpa-ONNX native library (37MB)
│   ├── Transforms/
│   │   └── Metadata.xml             # Java binding metadata fixes
│   └── Resources/raw/
│       └── sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/
│           ├── model.int8.onnx      # SenseVoice model (227MB)
│           └── tokens.txt            # Token definitions
└── claudedocs/                       # Claude-specific documentation
    ├── architecture-design.md        # Full architecture documentation
    ├── sherpa-onnx-integration.md    # Sherpa-ONNX integration guide
    ├── sherpa-onnx-setup.md          # Setup instructions
    └── implementation-status.md      # Implementation progress tracking
```

## Architecture Overview

### Dual Speech Recognition Strategy

The app supports **two speech recognition engines** selectable via `MainActivity._useSherpaOnnx`:

1. **Android Standard** (`VoiceInputService`):
   - System SpeechRecognizer API
   - Online, requires network
   - Has system beep sounds
   - Recognition occurs in segments with interruptions

2. **Sherpa-ONNX** (`SherpaRealtimeService`):
   - Fully offline operation
   - True continuous streaming recognition
   - No system sounds
   - Uses OfflineRecognizer with 3-second chunks
   - SenseVoice int8 model (Chinese, English, Japanese, Korean, Cantonese)

### Core Service Layer Architecture

**VoiceInputService** (Android Standard):
- Wraps Android SpeechRecognizer
- Event-driven: RecognitionStarted, RecognitionStopped, RecognitionResult, RecognitionError
- Supports continuous mode with automatic restart
- Handles permission requests for RECORD_AUDIO

**SherpaRealtimeService** (Sherpa-ONNX):
- Uses `Com.K2fsa.Sherpa.Onnx.OfflineRecognizer` via Java interop
- AudioRecord-based microphone input (16kHz, mono, PCM16)
- Chunk processing: buffers 3 seconds of audio, then recognizes
- Events: RecognitionStarted, RecognitionStopped, FinalResult, Error
- Async initialization required: `InitializeAsync(modelPath)`

**OpenAIService**:
- Chat Completion API integration
- Bearer token authentication
- JSON serialization with trimming suppression (IL2026)
- 60-second timeout for API calls

**ConversationService**:
- Manages message history (List<Message>)
- Fires MessageAdded event for UI updates
- Supports AddUserMessage, AddAIMessage, ClearHistory

### Data Flow

```
User taps mic button
  → MainActivity.OnMicButtonClick()
  → VoiceInputService.StartListening() OR SherpaRealtimeService.StartListening()
  → Speech recognized
  → RecognitionResult event fired
  → ConversationService.AddUserMessage(text)
  → OpenAIService.SendMessageAsync(conversationHistory)
  → API response received
  → ConversationService.AddAIMessage(response)
  → MessageAdapter.NotifyItemInserted()
  → RecyclerView updated
```

### UI Components

- **DrawerLayout**: Left navigation drawer with menu
- **RecyclerView**: Chat message list (user/AI messages)
- **FloatingActionButton**: Microphone button for voice input
- **StatusText**: Shows recognition state ("聞き取り中...", etc.)
- **SwipeHintView**: Custom hint overlay for drawer interaction

## Sherpa-ONNX Integration Details

### Java Binding Approach

Sherpa-ONNX uses **Java/Kotlin API** accessed via .NET Android Java interop, NOT C# bindings (which have known issues on Android).

**Key files:**
- `libs/sherpa-onnx-1.12.15.aar`: Android Archive with native libraries
- `Transforms/Metadata.xml`: Fixes Java binding issues (removes finalize() methods)
- `Robin.csproj`: References AAR with `<AndroidLibrary Include="..."/>`

**Native libraries included in AAR:**
- `libonnxruntime.so` (15-17MB)
- `libsherpa-onnx-jni.so` (2-4MB)
- `libsherpa-onnx-c-api.so`, `libsherpa-onnx-cxx-api.so`
- Supported ABIs: arm64-v8a, x86_64

### Model Configuration

Current model: **SenseVoice int8 multilingual**
- Path: `Resources/raw/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/`
- Size: ~227MB (int8 quantized)
- Languages: Chinese, English, Japanese, Korean, Cantonese
- Recognition approach: Offline batch processing in 3-second chunks

**Configuration object creation:**
```csharp
var senseVoiceConfig = new OfflineSenseVoiceModelConfig
{
    Model = $"{modelPath}/model.int8.onnx",
    Language = "auto",
    UseInverseTextNormalization = true
};

var modelConfig = new OfflineModelConfig
{
    Tokens = $"{modelPath}/tokens.txt",
    SenseVoice = senseVoiceConfig,
    NumThreads = 2,
    Debug = false,
    ModelType = "sense_voice"
};

var recognizerConfig = new OfflineRecognizerConfig
{
    FeatConfig = new FeatureConfig { SampleRate = 16000, FeatureDim = 80 },
    ModelConfig = modelConfig,
    DecodingMethod = "greedy_search"
};

_recognizer = new OfflineRecognizer(context.Assets, recognizerConfig);
```

### Known Issues and Workarounds

1. **Metadata.xml requirement**: Java `finalize()` methods cause C# binding errors. Fixed by removing them in `Transforms/Metadata.xml`.

2. **AAR duplicate library warnings**: Suppressed with `<NoWarn>XA4301</NoWarn>` in csproj.

3. **JSON trimming warnings**: Suppressed with `<NoWarn>IL2026</NoWarn>` - models are explicitly preserved.

4. **Chunk-based recognition**: Current implementation processes 3-second chunks rather than true streaming. Trade-off between latency and recognition accuracy.

## Development Workflow

### Adding New Features

1. **Check engine selection**: Determine if feature applies to Android standard, Sherpa-ONNX, or both
2. **Follow existing patterns**: Match event-driven architecture (EventHandler-based)
3. **Update MainActivity**: Wire up new service events in `InitializeServices()`
4. **Test both engines**: Verify with `_useSherpaOnnx = true` and `false`

### Modifying Speech Recognition

**For Android Standard (VoiceInputService):**
- Modify SpeechRecognizer intent extras in `StartListening()`
- Update error handling in `OnError` callback

**For Sherpa-ONNX (SherpaRealtimeService):**
- Adjust audio parameters: `SampleRate`, `ChunkDurationSeconds`, `BufferSizeInMs`
- Modify model config: `NumThreads`, `DecodingMethod`, `Language`
- Update `ProcessAudioChunk()` logic for different recognition patterns

### Working with OpenAI API

- API key management: Currently in OpenAIService constructor (needs secure storage)
- Model selection: Passed to `OpenAIService(apiKey, model)`
- Message history: Managed by ConversationService, sent as context to API
- Error handling: HttpRequestException for API errors, TaskCanceledException for timeouts

## Asset Management

### AndroidAsset Build Action and APK Packaging

**IMPORTANT**: Understanding the difference between project structure and APK structure is critical for Sherpa-ONNX integration.

**Project Structure** (source files):
```
src_dotnet/Robin/
└── Resources/raw/
    └── sherpa-onnx-ja-reazonspeech/
        ├── encoder-epoch-99-avg-1.int8.onnx
        ├── decoder-epoch-99-avg-1.int8.onnx
        ├── joiner-epoch-99-avg-1.int8.onnx
        └── tokens.txt
```

**APK Structure** (packaged files):
```
app.apk
└── assets/
    └── sherpa-onnx-ja-reazonspeech/
        ├── encoder-epoch-99-avg-1.int8.onnx
        ├── decoder-epoch-99-avg-1.int8.onnx
        ├── joiner-epoch-99-avg-1.int8.onnx
        └── tokens.txt
```

**Key Configuration** (Robin.csproj):
```xml
<AndroidAsset Include="Resources\raw\sherpa-onnx-ja-reazonspeech\encoder-epoch-99-avg-1.int8.onnx">
  <Link>assets\sherpa-onnx-ja-reazonspeech\encoder-epoch-99-avg-1.int8.onnx</Link>
</AndroidAsset>
```

- `Include`: Points to file in project
- `<Link>`: Specifies path in APK (must start with `assets\`)
- **Runtime reference**: `"sherpa-onnx-ja-reazonspeech"` (relative to assets/)

**Why `<Link>` is required**: Without `<Link>`, files would be packaged at `assets/Resources/raw/sherpa-onnx-ja-reazonspeech/`, which doesn't match the runtime path.

**Verification**:
```bash
unzip -l bin/Debug/net10.0-android/com.companyname.Robin.apk | grep "sherpa-onnx-ja-reazonspeech"
# Should show: assets/sherpa-onnx-ja-reazonspeech/*.onnx
```

See `claudedocs/dotnet-android-assets-guide.md` for comprehensive documentation.

## Critical Implementation Notes

### Engine Switching

The `_useSherpaOnnx` flag in MainActivity.cs (line 38) controls which engine is active:
```csharp
private bool _useSherpaOnnx = true; // true: Sherpa-ONNX, false: Android標準
```

This determines which service's events are connected to UI actions. Future enhancement: expose this in Settings UI.

### Resource Management

- **AudioRecord lifecycle**: Must call `Stop()` and `Release()` in Dispose
- **Sherpa recognizer**: Must dispose OfflineRecognizer and OfflineStream
- **Thread safety**: Audio buffer access protected with `_bufferLock`
- **UI thread**: Use `RunOnUiThread()` for UI updates from background threads

### Performance Considerations

**APK size:**
- Base app: ~5-10MB
- Sherpa-ONNX AAR: +37MB
- SenseVoice model: +227MB
- **Total: ~270MB installed**

**Runtime memory:**
- Model loading: ~300MB
- Active recognition: +100MB
- Total: ~400MB during use

**CPU usage:**
- Continuous recognition: 20-40% of one core
- Chunk processing reduces sustained load vs. true streaming

### Permissions

Required permissions in AndroidManifest.xml:
- `android.permission.RECORD_AUDIO`: Runtime permission required (API 23+)
- `android.permission.INTERNET`: For OpenAI API calls

Runtime permission request handled in `MainActivity.CheckPermissions()`.

## Testing Strategy

### Manual Testing Checklist

1. **Permission flow**: Verify RECORD_AUDIO permission request on first launch
2. **Android standard engine**: Test with `_useSherpaOnnx = false`
3. **Sherpa-ONNX engine**: Test with `_useSherpaOnnx = true`
4. **Continuous mode**: Verify auto-restart after recognition segments
5. **OpenAI integration**: Test message send/receive flow
6. **Drawer navigation**: Verify menu interactions
7. **Error handling**: Test network failures, microphone denials

### Key Test Scenarios

- Voice input in quiet environment (baseline accuracy)
- Voice input with background noise (robustness)
- Long conversation history (memory management)
- Rapid mic button tapping (state management)
- Device rotation during recognition (lifecycle)
- App backgrounding during recognition (cleanup)

## Documentation References

- Full architecture: `claudedocs/architecture-design.md`
- Sherpa-ONNX integration: `claudedocs/sherpa-onnx-integration.md`
- Setup guide: `claudedocs/sherpa-onnx-setup.md`
- Implementation status: `claudedocs/implementation-status.md`

## Common Development Patterns

### Adding Event Handlers

Follow this pattern for all service events:
```csharp
// In service class
public event EventHandler<string>? EventName;

// In MainActivity.InitializeServices()
_service = new Service(this);
_service.EventName += OnEventName;

// Handler implementation
private void OnEventName(object? sender, string data)
{
    RunOnUiThread(() => {
        // UI update code
    });
}
```

### Async/Await Pattern

Services use async initialization:
```csharp
Task.Run(async () =>
{
    var result = await _service.InitializeAsync(params);
    RunOnUiThread(() => UpdateUI(result));
});
```

### Null Safety

Project uses C# nullable reference types. Always check nullability:
```csharp
_service?.MethodCall();
if (_view != null) { _view.Update(); }
```

## Troubleshooting Common Issues

### Build Errors

**"Could not find sherpa-onnx-1.12.15.aar"**: Verify file exists in `src_dotnet/Robin/libs/`

**"Method 'finalize' not found"**: Ensure `Transforms/Metadata.xml` properly excludes finalize methods

**XA4301 warnings**: Expected due to duplicate libraries in AAR, suppressed in csproj

### Runtime Issues

**"Sherpa-ONNX 初期化失敗"**: Check model files exist in assets, verify paths in InitializeAsync

**Recognition not working**: Verify microphone permission granted, check logcat for errors

**OpenAI errors**: Validate API key, check network connectivity, verify message format

## Future Enhancement Areas

Based on implementation status:

1. **Settings UI**: Allow runtime engine switching, API key configuration
2. **True streaming**: Implement OnlineRecognizer instead of OfflineRecognizer chunks
3. **VAD integration**: Add voice activity detection to reduce unnecessary processing
4. **Model selection**: Support multiple Sherpa-ONNX models (accuracy vs. size trade-offs)
5. **Secure storage**: Move API key from code to encrypted preferences
6. **TTS integration**: Add text-to-speech for AI responses
7. **Offline mode**: Cache conversations, queue API calls when offline
