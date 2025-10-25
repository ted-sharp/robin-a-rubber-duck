# Robin.Core

Shared library for Robin voice chat application, providing common models and services for Sherpa-ONNX speech recognition.

## Overview

Robin.Core is a .NET 10.0 class library that centralizes model definitions and download logic for Sherpa-ONNX offline speech recognition models. This library is shared between:

- **ModelPrepTool** - PC-side model preparation console application
- **Robin** - Android MAUI voice chat application (future integration)

## Purpose

By extracting model definitions and download logic into a shared library, we achieve:

- **Single Source of Truth**: Model metadata (URLs, file lists, sizes) defined once
- **Code Reuse**: Same download and verification logic for PC tools and mobile apps
- **Maintainability**: Add/update models in one place, all tools benefit
- **Type Safety**: Compile-time validation of model configurations

## Components

### Models

#### `SherpaModelDefinition`
Defines available Sherpa-ONNX models with metadata:
- Model ID, name, languages supported
- Download URL and archive information
- Required files for verification
- Size information

**Usage:**
```csharp
using Robin.Core.Models;

// Get all Japanese-compatible models
var models = SherpaModelDefinition.JapaneseModels;

// Get specific model by ID
var model = SherpaModelDefinition.GetById("zipformer-ja-reazonspeech");
```

### Services

#### `ModelDownloader`
Handles model download and extraction:
- Downloads from GitHub releases
- Extracts `.tar.bz2` archives using system `tar`
- Progress reporting via events
- Skip already-downloaded models

**Usage:**
```csharp
using Robin.Core.Services;

var downloader = new ModelDownloader(outputDirectory);
downloader.ProgressChanged += (_, e) =>
{
    Console.WriteLine($"Progress: {e.ProgressPercentage:F1}%");
};

var modelPath = await downloader.DownloadAndPrepareAsync(model);
```

#### `ModelVerifier`
Verifies model integrity:
- Checks all required files exist
- Reports file sizes
- Calculates total model size

**Usage:**
```csharp
using Robin.Core.Services;

bool isValid = ModelVerifier.VerifyModel(modelPath, model);
var fileInfo = ModelVerifier.GetModelFileInfo(modelPath, model);
```

## Available Models

### SenseVoice Multilingual (int8)
- **ID**: `sense-voice-ja-zh-en`
- **Size**: ~238 MB (extracted)
- **Languages**: Japanese, Chinese, English, Korean, Cantonese
- **Use Case**: General purpose, multilingual support

### Zipformer Japanese ReazonSpeech
- **ID**: `zipformer-ja-reazonspeech`
- **Size**: ~680 MB (extracted)
- **Languages**: Japanese only
- **Use Case**: High accuracy Japanese-specific recognition

## Adding New Models

To add a new model, update `SherpaModelDefinition.JapaneseModels`:

```csharp
new SherpaModelDefinition
{
    Id = "my-new-model",
    Name = "My New Model",
    NameJa = "新しいモデル",
    Url = "https://github.com/.../model.tar.bz2",
    ArchiveFileName = "model.tar.bz2",
    FolderName = "model-folder",
    SizeBytes = 100L * 1024 * 1024,
    Languages = new[] { "Japanese" },
    RequiredFiles = new[] { "model.onnx", "tokens.txt" },
    Description = "Description here",
    SupportsJapanese = true
}
```

All tools using Robin.Core will automatically see the new model.

## Dependencies

- .NET 10.0
- System `tar` utility (for `.tar.bz2` extraction)
- HttpClient for downloads

## Usage in Projects

### ModelPrepTool
PC-side console application for downloading and preparing models.

```bash
dotnet add reference ../Robin.Core/Robin.Core.csproj
```

### Robin (Future)
Android MAUI app will reference this library for runtime model downloads.

```xml
<ProjectReference Include="..\Robin.Core\Robin.Core.csproj" />
```

## Build

```bash
dotnet build Robin.Core.csproj
```

## License

Part of the Robin voice chat application project.
