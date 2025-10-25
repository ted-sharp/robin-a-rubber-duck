namespace Robin.Core.Models;

/// <summary>
/// Sherpa-ONNX model definition with download metadata
/// </summary>
public class SherpaModelDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string NameJa { get; init; }
    public required string Url { get; init; }
    public required string ArchiveFileName { get; init; }
    public required string FolderName { get; init; }
    public required long SizeBytes { get; init; }
    public required string[] Languages { get; init; }
    public required string[] RequiredFiles { get; init; }
    public string? Description { get; init; }
    public bool SupportsJapanese { get; init; }

    public string SizeMB => $"{SizeBytes / (1024.0 * 1024.0):F1} MB";

    /// <summary>
    /// Available Sherpa-ONNX models for Japanese speech recognition
    /// </summary>
    public static readonly SherpaModelDefinition[] JapaneseModels = new[]
    {
        new SherpaModelDefinition
        {
            Id = "sense-voice-ja-zh-en",
            Name = "SenseVoice Multilingual",
            NameJa = "SenseVoice 多言語",
            Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09.tar.bz2",
            ArchiveFileName = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09.tar.bz2",
            FolderName = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
            SizeBytes = 238L * 1024 * 1024, // ~227MB compressed
            Languages = new[] { "Japanese", "Chinese", "English", "Korean", "Cantonese" },
            RequiredFiles = new[] { "model.int8.onnx", "tokens.txt" },
            Description = "Multilingual model with Japanese support, general purpose",
            SupportsJapanese = true
        },
        new SherpaModelDefinition
        {
            Id = "zipformer-ja-reazonspeech",
            Name = "Zipformer Japanese ReazonSpeech",
            NameJa = "Zipformer 日本語専用",
            Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01.tar.bz2",
            ArchiveFileName = "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01.tar.bz2",
            FolderName = "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
            SizeBytes = 680L * 1024 * 1024, // ~140MB compressed, ~680MB extracted
            Languages = new[] { "Japanese" },
            RequiredFiles = new[]
            {
                "encoder-epoch-99-avg-1.int8.onnx",
                "decoder-epoch-99-avg-1.int8.onnx",
                "joiner-epoch-99-avg-1.int8.onnx",
                "tokens.txt"
            },
            Description = "Japanese-only model with high accuracy",
            SupportsJapanese = true
        }
    };

    /// <summary>
    /// Get model by ID
    /// </summary>
    public static SherpaModelDefinition? GetById(string id)
    {
        return JapaneseModels.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all Japanese-compatible models
    /// </summary>
    public static IEnumerable<SherpaModelDefinition> GetJapaneseModels()
    {
        return JapaneseModels.Where(m => m.SupportsJapanese);
    }
}
