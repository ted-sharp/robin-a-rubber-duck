# Suggested Improvements to CLAUDE.md

The existing CLAUDE.md is comprehensive and well-structured. Here are recommended additions and improvements to incorporate the recent project development (Robin.Core and ModelPrepTool):

## 1. Add Multi-Project Context to Overview

**Current section**: "Project Overview"

**Suggested addition** (after technology stack):

```
**Multi-Project Structure:**
- **Robin** - MAUI Android voice chat application
- **Robin.Core** - Shared library (models, services, download logic)
- **ModelPrepTool** - PC console tool for preparing Sherpa-ONNX models
```

---

## 2. Expand "Build and Development Commands" Section

**Current**: Only lists Robin build commands

**Suggested improvements**:

### Add Solution-level commands:
```bash
# Build entire solution
dotnet build "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin.slnx"

# Clean all projects
dotnet clean "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin.slnx"
```

### Add Robin.Core commands:
```bash
# Build library
dotnet build "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\Robin.Core\Robin.Core.csproj"
```

### Add ModelPrepTool commands:
```bash
# List available models
dotnet run -p "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\ModelPrepTool" -- --list

# Download specific model
dotnet run -p "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\ModelPrepTool" -- --model sense-voice-ja-zh-en

# Download all models
dotnet run -p "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\ModelPrepTool" -- --model all

# Custom output directory
dotnet run -p "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\ModelPrepTool" -- --output D:\MyModels

# Clean archive files (keep extracted models)
dotnet run -p "C:\git\git-vo\robin-a-rubber-duck\src_dotnet\ModelPrepTool" -- --clean
```

### Enhance Device Management:
```bash
# Transfer model to device via adb
adb push models-prepared/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09 /sdcard/Download/sherpa-models/

# Verify files on device
adb shell "ls -lh /sdcard/Download/sherpa-models/"
```

---

## 3. Expand Project Structure

**Current**: Lists only Robin directory structure

**Suggested addition** after project structure diagram:

Add these to the root-level structure:
```
├── src_dotnet/Robin.Core/                # Shared library
│   ├── Models/
│   │   └── SherpaModelDefinition.cs      # Model metadata and URLs
│   └── Services/
│       ├── ModelDownloader.cs            # Download and extract logic
│       └── ModelVerifier.cs              # File verification
│
├── src_dotnet/ModelPrepTool/             # PC console tool
│   └── Program.cs                        # CLI interface
│
└── models-prepared/                      # Downloaded models (created by ModelPrepTool)
```

---

## 4. Add New Section: "Multi-Project Organization"

Insert after "Architecture Overview" heading, before "Dual Speech Recognition Strategy":

```markdown
### Multi-Project Organization

**Robin.Core** (Shared Library):
- Centralizes model definitions (URLs, metadata, file lists)
- Provides `ModelDownloader` and `ModelVerifier` for reuse
- Used by both ModelPrepTool and Robin application
- .NET 10.0 class library (platform-agnostic)

**ModelPrepTool** (Console Application):
- PC-side tool for downloading large models before Android deployment
- Downloads from GitHub releases (faster than mobile networks)
- Extracts `.tar.bz2` archives using system `tar`
- Supports batch operations and caching
- Uses Robin.Core for model definitions and download logic

**Robin** (Android Application):
- Main voice chat UI and service orchestration
- References Robin.Core for model definitions
- Integrates Sherpa-ONNX AAR for offline speech recognition
- Depends on models being available via device storage path
```

---

## 5. Add New Section: "Model Deployment Workflow"

Insert after "UI Components", before "Sherpa-ONNX Integration Details":

```markdown
## Model Deployment Workflow

### Download Models (PC Side)

1. **List available models**:
   ```bash
   dotnet run -p src_dotnet/ModelPrepTool -- --list
   ```

2. **Download models** (requires ~900MB disk space):
   ```bash
   dotnet run -p src_dotnet/ModelPrepTool -- --model all
   ```

3. **Models are extracted to** `models-prepared/`:
   - `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09/` (~238MB)
   - `sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01/` (~680MB)

### Transfer to Android Device

**Option 1: USB File Transfer (Simple)**
- Connect device via USB
- Copy model folder from `models-prepared/` to device `Internal Storage/Download/sherpa-models/`
- In Robin app: Settings → Model Path → Browse to folder

**Option 2: adb Push (Fast)**
```bash
adb push models-prepared/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09 /sdcard/Download/sherpa-models/
```

**Important**: Models are NOT bundled in APK. They must be on device storage, and app must be configured to use the correct path.
```

---

## 6. Enhance "Development Workflow" Section

**Add new subsection**: "Adding New Models to Robin.Core"

```markdown
### Adding New Models to Robin.Core

1. **Update SherpaModelDefinition.cs**:
   - Add new model entry with ID, name, languages, URL
   - Include all required files and their sizes
   - Update `JapaneseModels` collection if applicable

2. **ModelPrepTool will automatically support**:
   - List new model in `--list` output
   - Download new model via `--model [id]`
   - Verify all required files present

3. **Robin application will**:
   - See new model in any UI model selection (future)
   - Use same download path pattern
```

---

## 7. Expand "Troubleshooting Common Issues"

**Add ModelPrepTool-specific issues**:

```markdown
### ModelPrepTool Issues

**"tar: command not found"**: Ensure `tar` is in PATH. Windows 10+ includes tar by default.

**Download fails with timeout**: Increase timeout or check network connection. GitHub releases may be slow depending on location.

**"Model verification failed"**: Delete incomplete model folder and archive file, then re-run download.

**Disk space issues**: Clean archives after extraction with `--clean` flag.
```

**Add Model Deployment issues** (to existing Runtime Issues):

```markdown
**Model file not found**: Ensure ModelPrepTool was run to download models, verify transfer to device storage, check path in Robin app settings.
```

---

## 8. Update "Documentation References"

**Add**:
```markdown
- Robin.Core shared library: `src_dotnet/Robin.Core/README.md`
- ModelPrepTool: `src_dotnet/ModelPrepTool/README.md`
```

---

## 9. Update "Performance Considerations"

**Update APK size section**:
```
**APK size:**
- Base app: ~5-10MB
- Sherpa-ONNX AAR: +37MB
- SenseVoice model: +227MB
- **Total: ~270MB installed** (models not bundled in APK, must be downloaded separately)
```

---

## 10. Enhance "Future Enhancement Areas"

**Add to list**:
```markdown
4. **App runtime model download**: Replace PC-side tool with in-app download (requires large storage and good network)
...
9. **ModelPrepTool improvements**: Parallel downloads, SHA256 verification, direct adb push integration
```

---

## Summary of Changes

| Section | Change Type | Impact |
|---------|------------|--------|
| Overview | Addition | Introduces multi-project structure upfront |
| Build Commands | Expansion | Covers all three projects, not just Robin |
| Project Structure | Expansion | Shows Robin.Core and ModelPrepTool files |
| Architecture | Addition | Explains project organization and integration |
| Deployment | New section | Critical workflow docs for model setup |
| Development | Addition | Guide for adding new models |
| Troubleshooting | Expansion | ModelPrepTool and deployment issues |
| Documentation | Addition | References to Robin.Core and ModelPrepTool docs |
| Future Areas | Addition | ModelPrepTool enhancements |

**Result**: CLAUDE.md becomes a comprehensive guide for all three projects (Robin, Robin.Core, ModelPrepTool) with clear workflows for model deployment and development.
