namespace Robin.Core.Models;

/// <summary>
/// Qwen ONNX model definition with download metadata
/// </summary>
public class QwenModelDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string NameJa { get; init; }
    public required string RepositoryPath { get; init; }
    public required string FolderName { get; init; }
    public required long SizeBytes { get; init; }
    public required string[] Languages { get; init; }
    public required string[] RequiredFiles { get; init; }
    public string? Description { get; init; }
    public string? QuantizationType { get; init; }
    public int ParameterCount { get; init; }

    public string SizeMB => $"{SizeBytes / (1024.0 * 1024.0):F1} MB";

    /// <summary>
    /// Available Qwen ONNX models for text inference
    /// </summary>
    public static readonly QwenModelDefinition[] AvailableModels = new[]
    {
        new QwenModelDefinition
        {
            Id = "qwen-2.5-0.5b",
            Name = "Qwen 2.5 0.5B Instruct ONNX",
            NameJa = "Qwen 2.5 0.5B (軽量版)",
            RepositoryPath = "onnx-community/Qwen2.5-0.5B-Instruct",
            FolderName = "qwen25-0.5b-instruct",
            SizeBytes = 3_000_000_000, // ~3GB estimated (Transformers.js format + ONNX models)
            Languages = new[] { "Chinese", "English", "Japanese", "Korean" },
            RequiredFiles = new[]
            {
                "config.json",
                "generation_config.json",
                "tokenizer.json",
                "tokenizer_config.json",
                "special_tokens_map.json",
                "vocab.json",
                "merges.txt",
                "onnx/model.onnx",                       // フルプレシジョンモデル (推奨)
                "onnx/model_quantized.onnx"             // 量子化版（オプション）
            },
            Description = "Lightweight 0.5B parameter instruction-tuned model for mobile and embedded inference (Transformers.js ONNX format)",
            QuantizationType = "standard",
            ParameterCount = 500_000_000
        },
        new QwenModelDefinition
        {
            Id = "qwen-2.5-1.5b-int4",
            Name = "Qwen 2.5 1.5B ONNX (int4)",
            NameJa = "Qwen 2.5 1.5B (int4量子化)",
            RepositoryPath = "onnx-community/Qwen2.5-1.5B",
            FolderName = "qwen25-1.5b-int4",
            SizeBytes = 9_000_000_000, // ~9GB estimated (Transformers.js format + ONNX models)
            Languages = new[] { "Chinese", "English", "Japanese", "Korean", "Cantonese" },
            RequiredFiles = new[]
            {
                "config.json",
                "generation_config.json",
                "tokenizer.json",
                "tokenizer_config.json",
                "special_tokens_map.json",
                "vocab.json",
                "merges.txt",
                "onnx/model.onnx",                       // フルプレシジョンモデル
                "onnx/model_quantized.onnx"             // 量子化版（推奨）
            },
            Description = "1.5B parameter lightweight instruction-tuned model (Transformers.js ONNX format) for browser and mobile inference",
            QuantizationType = "int4",
            ParameterCount = 1_500_000_000
        }
    };

    /// <summary>
    /// Get model by ID
    /// </summary>
    public static QwenModelDefinition? GetById(string id)
    {
        return AvailableModels.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all available models
    /// </summary>
    public static IEnumerable<QwenModelDefinition> GetAllModels()
    {
        return AvailableModels;
    }
}
