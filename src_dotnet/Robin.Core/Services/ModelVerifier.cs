using Robin.Core.Models;

namespace Robin.Core.Services;

/// <summary>
/// Verifies model files and integrity
/// </summary>
public static class ModelVerifier
{
    /// <summary>
    /// Verify that all required Sherpa-ONNX model files exist
    /// </summary>
    public static bool VerifyModel(string modelPath, SherpaModelDefinition model)
    {
        if (!Directory.Exists(modelPath))
        {
            return false;
        }

        foreach (var requiredFile in model.RequiredFiles)
        {
            var filePath = Path.Combine(modelPath, requiredFile);
            if (!File.Exists(filePath))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Verify that all required Qwen ONNX model files exist
    /// </summary>
    public static bool VerifyModel(string modelPath, QwenModelDefinition model)
    {
        if (!Directory.Exists(modelPath))
        {
            return false;
        }

        foreach (var requiredFile in model.RequiredFiles)
        {
            var filePath = Path.Combine(modelPath, requiredFile);
            if (!File.Exists(filePath))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get file information for model files
    /// </summary>
    public static IEnumerable<ModelFileInfo> GetModelFileInfo(string modelPath, SherpaModelDefinition model)
    {
        foreach (var requiredFile in model.RequiredFiles)
        {
            var filePath = Path.Combine(modelPath, requiredFile);
            var exists = File.Exists(filePath);
            var sizeBytes = exists ? new FileInfo(filePath).Length : 0;

            yield return new ModelFileInfo
            {
                FileName = requiredFile,
                Exists = exists,
                SizeBytes = sizeBytes,
                SizeMB = sizeBytes / (1024.0 * 1024.0)
            };
        }
    }

    /// <summary>
    /// Get total size of extracted model
    /// </summary>
    public static long GetModelSize(string modelPath)
    {
        if (!Directory.Exists(modelPath))
        {
            return 0;
        }

        return new DirectoryInfo(modelPath)
            .GetFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}

public class ModelFileInfo
{
    public required string FileName { get; init; }
    public required bool Exists { get; init; }
    public required long SizeBytes { get; init; }
    public required double SizeMB { get; init; }
}
