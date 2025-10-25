using Robin.Core.Models;
using Robin.Core.Services;

namespace ModelPrepTool;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        try
        {
            var options = ParseArguments(args);

            if (options.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            var outputDir = options.OutputDirectory ?? Path.Combine(
                Path.GetDirectoryName(AppContext.BaseDirectory)!,
                "..", "..", "..", "..", "..", "models-prepared"
            );

            outputDir = Path.GetFullPath(outputDir);

            if (options.ListModels)
            {
                ShowModelList(outputDir);
                return 0;
            }

            if (options.CleanCache)
            {
                CleanArchives(outputDir);
                ShowSummary(outputDir);
                return 0;
            }

            // Select Sherpa models
            var sherpasToDownload = SelectSherpaModels(options.ModelId);
            var qwenToDownload = SelectQwenModels(options.ModelId);

            if (sherpasToDownload.Length == 0 && qwenToDownload.Length == 0)
            {
                Console.WriteLine("No models selected.");
                return 1;
            }

            Console.WriteLine("\n=== Model Preparation Tool ===");
            Console.WriteLine($"Output directory: {outputDir}\n");

            // Download Sherpa-ONNX and Qwen models
            var downloader = new ModelDownloader(outputDir);
            downloader.StatusChanged += (_, status) => Console.WriteLine($"  {status}");
            downloader.ProgressChanged += OnDownloadProgress;

            // Sherpa-ONNX models
            foreach (var model in sherpasToDownload)
            {
                Console.WriteLine($"\n=== Preparing: {model.Name} ===");
                Console.WriteLine($"Size: {model.SizeMB}");
                Console.WriteLine($"Languages: {string.Join(", ", model.Languages)}");

                try
                {
                    var modelPath = await downloader.DownloadAndPrepareAsync(model);
                    Console.WriteLine($"\n[OK] Model prepared at: {modelPath}");

                    ShowSetupInstructions(modelPath, model.FolderName);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR] Failed to prepare {model.Name}: {ex.Message}");
                    Console.ResetColor();
                    return 1;
                }
            }

            // Qwen models
            foreach (var model in qwenToDownload)
            {
                Console.WriteLine($"\n=== Preparing: {model.Name} ===");
                Console.WriteLine($"Size: {model.SizeMB}");
                Console.WriteLine($"Languages: {string.Join(", ", model.Languages)}");
                Console.WriteLine($"Type: {model.QuantizationType} quantization");

                try
                {
                    var modelPath = await downloader.DownloadAndPrepareAsync(model);
                    Console.WriteLine($"\n[OK] Model prepared at: {modelPath}");

                    ShowQwenSetupInstructions(modelPath, model.FolderName);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR] Failed to prepare {model.Name}: {ex.Message}");
                    Console.ResetColor();
                    return 1;
                }
            }

            Console.WriteLine("\n=== Preparation Complete! ===");
            ShowSummary(outputDir);

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    static void OnDownloadProgress(object? sender, DownloadProgressEventArgs e)
    {
        var downloadedMB = e.BytesDownloaded / (1024.0 * 1024.0);
        var totalMB = e.TotalBytes > 0 ? e.TotalBytes / (1024.0 * 1024.0) : 0;

        Console.Write($"\r  Progress: {e.ProgressPercentage:F1}% ({downloadedMB:F1} / {totalMB:F1} MB) @ {e.SpeedMBps:F2} MB/s   ");
    }

    static SherpaModelDefinition[] SelectSherpaModels(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId) || modelId == "all")
        {
            return SherpaModelDefinition.JapaneseModels;
        }

        // Check if it's a Qwen model
        if (QwenModelDefinition.GetById(modelId) != null)
        {
            return Array.Empty<SherpaModelDefinition>();
        }

        var model = SherpaModelDefinition.GetById(modelId);
        if (model == null)
        {
            Console.WriteLine($"Unknown model ID: {modelId}");
            Console.WriteLine("Available Sherpa-ONNX models:");
            foreach (var m in SherpaModelDefinition.JapaneseModels)
            {
                Console.WriteLine($"  - {m.Id}");
            }
            Console.WriteLine("Available Qwen models:");
            foreach (var m in QwenModelDefinition.AvailableModels)
            {
                Console.WriteLine($"  - {m.Id}");
            }
            return Array.Empty<SherpaModelDefinition>();
        }

        return new[] { model };
    }

    static QwenModelDefinition[] SelectQwenModels(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId) || modelId == "all")
        {
            return QwenModelDefinition.AvailableModels;
        }

        // Check if it's a Sherpa model
        if (SherpaModelDefinition.GetById(modelId) != null)
        {
            return Array.Empty<QwenModelDefinition>();
        }

        var model = QwenModelDefinition.GetById(modelId);
        return model == null ? Array.Empty<QwenModelDefinition>() : new[] { model };
    }

    static void ShowModelList(string outputDir)
    {
        Console.WriteLine("\n=== Sherpa-ONNX Speech Recognition Models ===\n");

        foreach (var model in SherpaModelDefinition.JapaneseModels)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{model.Id}]");
            Console.ResetColor();

            Console.WriteLine($"  Name: {model.Name}");
            Console.WriteLine($"  Size: {model.SizeMB}");
            Console.WriteLine($"  Languages: {string.Join(", ", model.Languages)}");
            Console.WriteLine($"  Description: {model.Description}");

            var modelPath = Path.Combine(outputDir, model.FolderName);
            var status = Directory.Exists(modelPath) && ModelVerifier.VerifyModel(modelPath, model)
                ? "Ready"
                : "Not prepared";

            var statusColor = status == "Ready" ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.ForegroundColor = statusColor;
            Console.WriteLine($"  Status: {status}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }

    static void ShowSummary(string outputDir)
    {
        Console.WriteLine("\n=== Summary ===\n");

        var totalSize = 0L;
        var preparedCount = 0;

        foreach (var model in SherpaModelDefinition.JapaneseModels)
        {
            var modelPath = Path.Combine(outputDir, model.FolderName);
            if (Directory.Exists(modelPath) && ModelVerifier.VerifyModel(modelPath, model))
            {
                var size = ModelVerifier.GetModelSize(modelPath);
                var sizeMB = size / (1024.0 * 1024.0);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [Ready] {model.Name} ({sizeMB:F1} MB)");
                Console.ResetColor();

                totalSize += size;
                preparedCount++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"  [Not prepared] {model.Name}");
                Console.ResetColor();
            }
        }

        if (preparedCount > 0)
        {
            var totalMB = totalSize / (1024.0 * 1024.0);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTotal: {preparedCount} models prepared ({totalMB:F1} MB)");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"\nStorage: {outputDir}");
        Console.ResetColor();
    }

    static void ShowSetupInstructions(string modelPath, string folderName)
    {
        Console.WriteLine("\n=== Device Setup Instructions ===");
        Console.WriteLine("\nPrepared model: {0}", modelPath);
        Console.WriteLine("\nChoose transfer method:\n");

        Console.WriteLine("[Option 1] USB File Transfer (Easy)");
        Console.WriteLine("  1. Connect Android device via USB");
        Console.WriteLine("  2. Open device in File Explorer");
        Console.WriteLine("  3. Copy model folder to: Internal Storage/Download/sherpa-models/");
        Console.WriteLine("  4. In Robin app: Settings → Model Path → Browse and select");

        Console.WriteLine("\n[Option 2] adb Push (Fast)");
        Console.WriteLine($"  adb push \"{modelPath}\" /sdcard/Download/sherpa-models/{folderName}");
        Console.WriteLine("  # Verify:");
        Console.WriteLine($"  adb shell \"ls -lh /sdcard/Download/sherpa-models/{folderName}\"");

        Console.WriteLine("\n[Option 3] App Runtime Download (Future)");
        Console.WriteLine("  Robin app will support downloading models directly from device");
        Console.WriteLine("  This PC preparation ensures models are ready for quick transfer");
    }

    static void CleanArchives(string outputDir)
    {
        Console.WriteLine("\nCleaning archive files...");

        if (!Directory.Exists(outputDir))
        {
            Console.WriteLine("No archives to clean");
            return;
        }

        var archives = Directory.GetFiles(outputDir, "*.tar.bz2");
        foreach (var archive in archives)
        {
            File.Delete(archive);
            Console.WriteLine($"  Removed: {Path.GetFileName(archive)}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[OK] Cleanup complete (extracted models preserved)");
        Console.ResetColor();
    }

    static ProgramOptions ParseArguments(string[] args)
    {
        var options = new ProgramOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    options.ShowHelp = true;
                    break;
                case "--list" or "-l":
                    options.ListModels = true;
                    break;
                case "--clean" or "-c":
                    options.CleanCache = true;
                    break;
                case "--model" or "-m":
                    if (i + 1 < args.Length)
                    {
                        options.ModelId = args[++i];
                    }
                    break;
                case "--output" or "-o":
                    if (i + 1 < args.Length)
                    {
                        options.OutputDirectory = args[++i];
                    }
                    break;
            }
        }

        return options;
    }

    static void ShowQwenSetupInstructions(string modelPath, string folderName)
    {
        Console.WriteLine("\n=== Device Setup Instructions (Qwen) ===");
        Console.WriteLine("\nPrepared model: {0}", modelPath);
        Console.WriteLine("\nThe Qwen ONNX model is ready for use with the Robin app.");
        Console.WriteLine("\nUsage:\n");

        Console.WriteLine("[Option 1] Use directly from PC (Development)");
        Console.WriteLine("  1. Configure Robin app to use this model path");
        Console.WriteLine("  2. Model path: {0}", modelPath);

        Console.WriteLine("\n[Option 2] Transfer to Android Device");
        Console.WriteLine("  1. Connect Android device via USB");
        Console.WriteLine("  2. Open device in File Explorer");
        Console.WriteLine("  3. Copy model folder to: Internal Storage/Download/robin-models/");
        Console.WriteLine("  4. In Robin app: Configure model path or auto-download feature");

        Console.WriteLine("\n[Option 3] adb Push");
        Console.WriteLine($"  adb push \"{modelPath}\" /sdcard/Download/robin-models/{folderName}");
        Console.WriteLine("  # Verify:");
        Console.WriteLine($"  adb shell \"ls -lh /sdcard/Download/robin-models/{folderName}\"");
    }

    static void ShowHelp()
    {
        Console.WriteLine(@"
Model Preparation Tool

Usage:
  ModelPrepTool [options]

Options:
  --help, -h              Show this help message
  --list, -l              List available models and their status
  --clean, -c             Clean archive files (keep extracted models)
  --model, -m <id>        Prepare specific model (default: all)
  --output, -o <path>     Output directory (default: ../models-prepared)

Examples:
  ModelPrepTool --list
  ModelPrepTool --model sense-voice-ja-zh-en
  ModelPrepTool --model zipformer-ja-reazonspeech
  ModelPrepTool --model whisper-tiny
  ModelPrepTool --model nemo-parakeet-cja
  ModelPrepTool --model qwen-2.5-0.5b
  ModelPrepTool --model qwen-2.5-1.5b-int4
  ModelPrepTool --model all
  ModelPrepTool --clean

Available Model IDs:

  Sherpa-ONNX (Speech Recognition):
    sense-voice-ja-zh-en           SenseVoice Multilingual
    zipformer-ja-reazonspeech      Zipformer Japanese ReazonSpeech
    whisper-tiny                   Whisper Tiny (Multilingual, ~104MB)
    nemo-parakeet-cja              NeMo Parakeet CTC 0.6B Japanese (~625MB)

  Qwen (Text Inference):
    qwen-2.5-0.5b                  Qwen 2.5 0.5B (Lightweight, ~3GB)
    qwen-2.5-1.5b-int4             Qwen 2.5 1.5B (Standard, ~9GB)

  Special:
    all                            All models (default)
");
    }

    class ProgramOptions
    {
        public bool ShowHelp { get; set; }
        public bool ListModels { get; set; }
        public bool CleanCache { get; set; }
        public string? ModelId { get; set; }
        public string? OutputDirectory { get; set; }
    }
}
