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
        },
        new SherpaModelDefinition
        {
            Id = "whisper-tiny",
            Name = "Whisper Tiny (Multilingual, int8)",
            NameJa = "Whisper Tiny (多言語、int8量子化)",
            Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-whisper-tiny.tar.bz2",
            ArchiveFileName = "sherpa-onnx-whisper-tiny.tar.bz2",
            FolderName = "sherpa-onnx-whisper-tiny",
            SizeBytes = 104L * 1024 * 1024, // ~104MB (int8 quantized encoder + decoder)
            Languages = new[] { "Japanese", "English", "Chinese", "Korean", "Spanish", "French", "German", "Italian", "Portuguese", "Dutch", "Russian", "Polish", "Turkish", "Swedish", "Norwegian", "Danish", "Finnish", "Czech", "Romanian", "Greek" },
            RequiredFiles = new[]
            {
                "tiny-encoder.int8.onnx",
                "tiny-decoder.int8.onnx",
                "tiny-tokens.txt"
            },
            Description = "Fast multilingual speech recognition model based on Whisper (99 languages supported, int8 quantized)",
            SupportsJapanese = true
        },
        new SherpaModelDefinition
        {
            Id = "nemo-parakeet-cja",
            Name = "NeMo Parakeet CTC 0.6B Japanese (int8)",
            NameJa = "NeMo Parakeet CTC 0.6B 日本語専用",
            Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8.tar.bz2",
            ArchiveFileName = "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8.tar.bz2",
            FolderName = "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8",
            SizeBytes = 625L * 1024 * 1024, // ~625MB extracted
            Languages = new[] { "Japanese" },
            RequiredFiles = new[]
            {
                "model.int8.onnx",
                "tokens.txt"
            },
            Description = "Lightweight Japanese speech recognition model trained on 35,000+ hours of ReazonSpeech v2.0. Smaller alternative to Zipformer with simpler CTC architecture",
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
