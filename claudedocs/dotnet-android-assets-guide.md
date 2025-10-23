# .NET Android Asset Management Guide

**Updated**: 2025-10-24
**Project**: Robin Android App
**Framework**: .NET 10.0-android

## Overview

This document explains the correct way to manage Android assets in .NET Android projects, clarifying the confusion between `Resources/`, `Assets/`, and APK packaging paths.

## Key Concepts

### Build Actions

**For .NET Android (non-MAUI)**:
- Use `AndroidAsset` build action
- Files are packaged into APK's `assets/` folder
- Accessed at runtime via `AssetManager` API

**For .NET MAUI**:
- Use `MauiAsset` build action (for cross-platform resources)
- Use `AndroidAsset` for Android-specific assets

### Project Structure vs APK Structure

**Project Structure** (where files live in source code):
```
MyApp/
├── Resources/
│   └── raw/
│       └── sherpa-onnx-ja-reazonspeech/
│           ├── encoder-epoch-99-avg-1.int8.onnx
│           ├── decoder-epoch-99-avg-1.int8.onnx
│           ├── joiner-epoch-99-avg-1.int8.onnx
│           └── tokens.txt
```

**APK Structure** (where files are packaged):
```
app.apk
└── assets/
    └── sherpa-onnx-ja-reazonspeech/
        ├── encoder-epoch-99-avg-1.int8.onnx
        ├── decoder-epoch-99-avg-1.int8.onnx
        ├── joiner-epoch-99-avg-1.int8.onnx
        └── tokens.txt
```

## The `<Link>` Metadata

The `<Link>` metadata controls **where files are placed in the APK**, regardless of their location in the project.

### Syntax

```xml
<AndroidAsset Include="[source-path]">
  <Link>[apk-destination-path]</Link>
</AndroidAsset>
```

### Example from Robin Project

```xml
<AndroidAsset Include="Resources\raw\sherpa-onnx-ja-reazonspeech\encoder-epoch-99-avg-1.int8.onnx">
  <Link>assets\sherpa-onnx-ja-reazonspeech\encoder-epoch-99-avg-1.int8.onnx</Link>
</AndroidAsset>
```

**Explanation**:
- `Include`: Points to file in project: `Resources/raw/sherpa-onnx-ja-reazonspeech/encoder-*.onnx`
- `<Link>`: Specifies APK path: `assets/sherpa-onnx-ja-reazonspeech/encoder-*.onnx`
- **Note**: `<Link>` path starts with `assets\` because that's the root in the APK

## Runtime Access

### C# Code Reference

```csharp
// In MainActivity or Service
string modelPath = "sherpa-onnx-ja-reazonspeech";

// Sherpa-ONNX uses AssetManager internally via context.Assets
var config = new OfflineRecognizerConfig { ... };
_recognizer = new OfflineRecognizer(context.Assets, config);
```

### How Sherpa-ONNX Accesses Files

Sherpa-ONNX Java binding internally constructs paths like:
```
{modelPath}/encoder-epoch-99-avg-1.int8.onnx
→ sherpa-onnx-ja-reazonspeech/encoder-epoch-99-avg-1.int8.onnx
```

And accesses via `AssetManager.Open("sherpa-onnx-ja-reazonspeech/encoder-epoch-99-avg-1.int8.onnx")`

## Common Pitfalls

### ❌ Wrong: Direct File Paths

```csharp
// This DOES NOT WORK - assets are not accessible via file system
string path = "/data/data/com.app/assets/model.onnx";
```

### ❌ Wrong: Missing Link Metadata

```xml
<!-- Without Link, files end up at: assets/Resources/raw/sherpa-onnx-ja-reazonspeech/ -->
<AndroidAsset Include="Resources\raw\sherpa-onnx-ja-reazonspeech\encoder.onnx" />
```

### ✅ Correct: Link Metadata

```xml
<!-- With Link, files are at: assets/sherpa-onnx-ja-reazonspeech/ -->
<AndroidAsset Include="Resources\raw\sherpa-onnx-ja-reazonspeech\encoder.onnx">
  <Link>assets\sherpa-onnx-ja-reazonspeech\encoder.onnx</Link>
</AndroidAsset>
```

### ✅ Correct: AssetManager Access

```csharp
// Use AssetManager API
using (var stream = context.Assets.Open("sherpa-onnx-ja-reazonspeech/model.onnx"))
{
    // Process stream
}
```

## Alternative: Traditional Assets Folder

For simpler projects, you can use the traditional `Assets/` folder:

**Project Structure**:
```
MyApp/
├── Assets/
│   └── models/
│       └── model.onnx
```

**csproj**: (No Link needed - files automatically go to APK assets/)
```xml
<AndroidAsset Include="Assets\models\model.onnx" />
```

**Runtime Access**:
```csharp
string modelPath = "models";
```

## Asset Packs (.NET 9+)

For large files (>150MB), use Asset Packs:

```xml
<AndroidAsset Include="Resources\raw\large-model.onnx"
               AssetPack="myassets"
               DeliveryType="FastFollow">
  <Link>assets\large-model.onnx</Link>
</AndroidAsset>
```

**Delivery Types**:
- `InstallTime`: Included in APK (default)
- `FastFollow`: Downloaded after app install
- `OnDemand`: Downloaded when requested

## Best Practices

1. **Use `<Link>` for custom paths**: Always specify `<Link>` when files are outside the `Assets/` folder
2. **Keep APK paths simple**: Avoid deep nesting in APK structure
3. **Group related files**: Keep model files in same directory
4. **Check APK contents**: Use `unzip -l app.apk | grep assets` to verify packaging
5. **Document paths**: Clearly document the mapping in CLAUDE.md

## Verification Commands

```bash
# Check APK structure
unzip -l bin/Debug/net10.0-android/app.apk | grep "sherpa"

# Expected output:
# assets/sherpa-onnx-ja-reazonspeech/encoder-epoch-99-avg-1.int8.onnx
# assets/sherpa-onnx-ja-reazonspeech/decoder-epoch-99-avg-1.int8.onnx
# assets/sherpa-onnx-ja-reazonspeech/joiner-epoch-99-avg-1.int8.onnx
# assets/sherpa-onnx-ja-reazonspeech/tokens.txt
```

## References

- [.NET Android Build Items](https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-items)
- [Android Asset Packs](https://learn.microsoft.com/en-us/dotnet/maui/android/asset-packs)
- [Using Android Assets (Xamarin)](https://learn.microsoft.com/en-us/xamarin/android/app-fundamentals/resources-in-android/android-assets)

## Project-Specific Implementation

**File**: `Robin.csproj`
**Pattern**: `Resources/raw/*` → `assets/*` via `<Link>` metadata
**Runtime Path**: `"sherpa-onnx-ja-reazonspeech"` (relative to `assets/`)

This approach allows flexible project organization while maintaining clean APK structure.
