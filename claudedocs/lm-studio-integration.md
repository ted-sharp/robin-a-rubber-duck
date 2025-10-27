# LM Studio Integration Guide

This guide explains how to set up and use LM Studio with the Robin application for local LLM inference.

## Overview

The Robin app now supports connecting to LM Studio, a local LLM inference platform. This allows you to run large language models completely offline on your PC, without relying on cloud APIs.

**Key features:**
- ✅ Local model inference (no internet required)
- ✅ Easy configuration through the Settings UI
- ✅ Automatic fallback to mock mode if connection fails
- ✅ Compatible with any OpenAI-compatible API endpoint
- ✅ Persistent settings storage

## Architecture

### Components

1. **LMStudioSettings** (`Models/LMStudioSettings.cs`)
   - Data model for storing LM Studio configuration
   - Properties: Endpoint, ModelName, IsEnabled

2. **SettingsService** (`Services/SettingsService.cs`)
   - Manages settings persistence using Android SharedPreferences
   - Methods: LoadLMStudioSettings(), SaveLMStudioSettings(), ClearLMStudioSettings()

3. **OpenAIService** (`Services/OpenAIService.cs`)
   - Enhanced with LM Studio support
   - New constructor: `OpenAIService(endpoint, modelName, isLMStudio: true)`
   - Handles both OpenAI API and LM Studio endpoints

4. **SettingsActivity** (`SettingsActivity.cs`)
   - Android Activity for LM Studio configuration UI
   - Layout: `Resources/layout/activity_settings.xml`

5. **MainActivity Integration**
   - Loads settings on startup
   - Initializes OpenAIService with LM Studio if configured
   - Menu option to access SettingsActivity

## Setup Instructions

### Step 1: Install and Configure LM Studio on PC

1. Download and install [LM Studio](https://lmstudio.ai/)
2. Launch LM Studio
3. Download a model (e.g., mistral-7b-instruct-v0.1-gguf)
4. Load the model in LM Studio
5. Start the local API server (default: http://localhost:8000)

### Step 2: Configure in Robin App

1. Open Robin app on your Android device
2. Tap the menu button (三 icon in drawer)
3. Select "LM Studio設定" (LM Studio Settings)
4. Enable "LM Studio を有効にする" checkbox
5. Enter the endpoint URL (e.g., `http://192.168.1.100:8000` - use your PC's IP on the network)
6. Enter the model name (matching the model loaded in LM Studio)
7. Tap "保存" (Save)

### Step 3: Test Connection

1. Return to the chat screen
2. Speak into the microphone
3. Check if the response comes from LM Studio
4. If connection fails, the app will fall back to mock mode

## Network Configuration

### Local Network Setup

For LM Studio running on your PC to be accessible from your Android device:

**Option 1: WiFi Network (Recommended)**
```
PC:     LM Studio running on http://localhost:8000
Device: Connect to same WiFi network
Robin:  Use PC's local IP (e.g., http://192.168.1.100:8000)
```

**Option 2: USB Debugging with Port Forwarding**
```bash
adb forward tcp:8000 tcp:8000
Robin: Use http://localhost:8000
```

### Finding Your PC's IP Address

**Windows:**
```powershell
ipconfig
# Look for "IPv4 Address" under your WiFi adapter
```

**macOS/Linux:**
```bash
ifconfig | grep inet
```

## API Compatibility

The implementation uses the OpenAI Chat Completions API format, which is widely supported:

- LM Studio (native support)
- Ollama (with OpenAI-compatible endpoint)
- text-generation-webui (with OpenAI API extension)
- Any OpenAI-compatible server

### Supported Request Format

```json
{
  "model": "model-name",
  "messages": [
    {"role": "user", "content": "Hello"},
    {"role": "assistant", "content": "Hi there!"}
  ],
  "temperature": 0.7
}
```

## Code Details

### Adding LM Studio Service

```csharp
// In MainActivity.InitializeServices()
var lmStudioSettings = _settingsService.LoadLMStudioSettings();

if (lmStudioSettings.IsEnabled && !string.IsNullOrWhiteSpace(lmStudioSettings.ModelName))
{
    _openAIService = new OpenAIService(
        lmStudioSettings.Endpoint,
        lmStudioSettings.ModelName,
        isLMStudio: true
    );
}
```

### OpenAIService Constructor for LM Studio

```csharp
public OpenAIService(string endpoint, string model, bool isLMStudio)
{
    _apiKey = "lm-studio"; // No API key needed
    _model = model;
    _baseAddress = endpoint.TrimEnd('/') + "/v1/";
    _httpClient = new HttpClient
    {
        BaseAddress = new Uri(_baseAddress),
        Timeout = TimeSpan.FromSeconds(60)
    };
    // No Authorization header for LM Studio
}
```

### SharedPreferences Storage

Settings are automatically persisted:
```
PreferencesFile: robin_settings
Keys:
  - lm_studio_endpoint
  - lm_studio_model_name
  - lm_studio_enabled
```

## UI Features

### Settings Screen

The SettingsActivity provides:
- ✅ Enable/Disable toggle
- ✅ Endpoint URL input field (with validation)
- ✅ Model name input field
- ✅ Usage instructions
- ✅ Save button with validation

### Validation

- Endpoint URL is validated for HTTP/HTTPS prefix
- Model name is required if LM Studio is enabled
- Empty fields trigger toast notifications with error messages

## Troubleshooting

### "LM Studio接続失敗" (LM Studio connection failed)

**Causes:**
- LM Studio is not running on the PC
- Endpoint URL is incorrect
- Network connectivity issue between device and PC
- API server is not started in LM Studio

**Solutions:**
1. Verify LM Studio is running on your PC
2. Check endpoint URL (use PC's local IP, not localhost)
3. Test PC connectivity: `ping <pc-ip>`
4. Verify API server started in LM Studio UI

### Slow Response Time

**Causes:**
- Model is too large for your PC
- Network latency
- PC CPU/GPU is under load

**Solutions:**
1. Use a smaller model (e.g., mistral-7b instead of 13b)
2. Check WiFi signal strength
3. Reduce other applications running on PC
4. Enable GPU acceleration in LM Studio if available

### App Crashes When Sending Message

**Causes:**
- LM Studio API is not compatible
- Endpoint format is wrong
- Model name mismatch

**Solutions:**
1. Test LM Studio API with curl: `curl http://localhost:8000/v1/models`
2. Verify endpoint format: `http://host:port` (no trailing slash)
3. Check model name matches exactly in LM Studio

## Future Enhancements

Potential improvements for LM Studio integration:

1. **Model Selection Dialog** - Browse available models from LM Studio
2. **Connection Testing** - Test endpoint connectivity before saving
3. **API Key Support** - Allow configuration of API keys for compatible endpoints
4. **Response Streaming** - Stream responses from LM Studio for faster feedback
5. **Model Parameters** - Expose temperature, top_p, and other parameters
6. **Connection History** - Remember multiple endpoint configurations

## Example Models

Recommended models for smooth operation:

| Model | Size | Speed | Quality | Notes |
|-------|------|-------|---------|-------|
| mistral-7b-instruct | 4.1GB | Fast | Good | Excellent balance |
| neural-chat-7b | 3.8GB | Fast | Good | Optimized for chat |
| openchat-3.5 | 4.4GB | Fast | Good | Faster inference |
| llama-2-13b-chat | 7.4GB | Medium | Excellent | Better quality |
| neural-chat-13b | 7.4GB | Medium | Excellent | Better understanding |

Note: Actual model names in LM Studio may differ slightly. Use the exact name shown in the LM Studio UI.

## References

- [LM Studio Official Website](https://lmstudio.ai/)
- [OpenAI Chat Completions API](https://platform.openai.com/docs/api-reference/chat)
- Robin Project Documentation: `/claudedocs/`
