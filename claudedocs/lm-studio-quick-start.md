# LM Studio Quick Start

Get LM Studio running with Robin in 5 minutes.

## What You Need

- ✅ LM Studio installed on your PC
- ✅ Android device on the same WiFi network
- ✅ Robin app (v1.0+)

## PC Setup (2 minutes)

```
1. Open LM Studio
2. Click "Search Models" → Find a model (e.g., Mistral 7B)
3. Download the model
4. Click "Load Model" when ready
5. Go to "Local Server" tab
6. Click "Start Server" (default: http://localhost:8000)
7. Verify: Server shows "Ready" status
```

## Android Setup (3 minutes)

```
1. Open Robin app
2. Open drawer menu (← swipe from left edge)
3. Tap "LM Studio設定" (Settings)
4. Enable checkbox
5. Endpoint: http://[YOUR_PC_IP]:8000
   (Find PC IP: Windows cmd → ipconfig → IPv4 Address)
6. Model name: [Model name from LM Studio]
7. Tap "保存" (Save)
```

## Test It

```
1. Return to chat
2. Speak your question
3. Wait for LM Studio response
4. Done! 🎉
```

## Find Your PC's IP

**Windows:**
```
Start Menu → cmd → ipconfig → Look for "IPv4 Address"
Example: 192.168.1.100
```

**macOS:**
```
System Preferences → Network → See your IP address
```

**Linux:**
```
Terminal → hostname -I
```

## Example Configuration

```
Endpoint: http://192.168.1.100:8000
Model: mistral-7b-instruct-v0.1-gguf
```

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Can't reach endpoint | Check PC IP, use WiFi (not mobile data) |
| "Connection failed" | Make sure LM Studio server is running |
| No response from model | Check model name matches exactly |
| Slow responses | Close other apps, try smaller model |

## What Happens

```
You: "こんにちは"
     ↓ (speech recognition)
Robin: [text to OpenAI Service]
     ↓ (sends to LM Studio)
LM Studio: [runs model locally]
     ↓ (returns response)
Robin: "こんにちは。お疲れ様です。"
     ↓ (displays answer)
You: [read response]
```

## Next Steps

- For advanced configuration, see: `lm-studio-integration.md`
- For architecture details, see: `architecture-design.md`
- For troubleshooting, see: `lm-studio-integration.md#troubleshooting`
