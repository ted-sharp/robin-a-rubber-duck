using System.Diagnostics;
using Robin.Core.Models;

namespace Robin.Core.Services;

/// <summary>
/// Downloads and extracts Sherpa-ONNX models
/// </summary>
public class ModelDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _outputDirectory;

    public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
    public event EventHandler<string>? StatusChanged;

    public ModelDownloader(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    /// <summary>
    /// Download and prepare Sherpa-ONNX model (skip if already exists)
    /// </summary>
    public async Task<string> DownloadAndPrepareAsync(
        SherpaModelDefinition model,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_outputDirectory);

        var extractPath = Path.Combine(_outputDirectory, model.FolderName);

        // Skip if already extracted
        if (Directory.Exists(extractPath) && ModelVerifier.VerifyModel(extractPath, model))
        {
            OnStatusChanged($"[SKIP] Model already prepared: {model.FolderName}");
            return extractPath;
        }

        var archivePath = Path.Combine(_outputDirectory, model.ArchiveFileName);

        // Download if needed
        if (!File.Exists(archivePath))
        {
            OnStatusChanged($"Downloading {model.Name}...");
            await DownloadFileAsync(model.Url, archivePath, cancellationToken);
            OnStatusChanged("Download complete");
        }
        else
        {
            OnStatusChanged($"[SKIP] Archive exists: {model.ArchiveFileName}");
        }

        // Extract
        OnStatusChanged("Extracting archive...");
        await ExtractArchiveAsync(archivePath, _outputDirectory, cancellationToken);
        OnStatusChanged("Extraction complete");

        // Verify
        OnStatusChanged("Verifying files...");
        if (!ModelVerifier.VerifyModel(extractPath, model))
        {
            throw new InvalidOperationException($"Model verification failed: {model.Id}");
        }
        OnStatusChanged("Verification complete");

        return extractPath;
    }

    /// <summary>
    /// Download and prepare Qwen ONNX model from Hugging Face
    /// </summary>
    public async Task<string> DownloadAndPrepareAsync(
        QwenModelDefinition model,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_outputDirectory);

        var modelPath = Path.Combine(_outputDirectory, model.FolderName);

        // Skip if already downloaded
        if (Directory.Exists(modelPath) && ModelVerifier.VerifyModel(modelPath, model))
        {
            OnStatusChanged($"[SKIP] Model already prepared: {model.FolderName}");
            return modelPath;
        }

        Directory.CreateDirectory(modelPath);

        // Download each required file from Hugging Face
        long totalBytes = 0;
        long downloadedBytes = 0;

        // First pass: calculate total size
        OnStatusChanged("Checking file sizes...");
        foreach (var file in model.RequiredFiles)
        {
            var fileSize = await GetFileSizeAsync(model, file, cancellationToken);
            totalBytes += fileSize;
        }

        // Second pass: download files
        foreach (var file in model.RequiredFiles)
        {
            var fileSize = await GetFileSizeAsync(model, file, cancellationToken);
            var url = BuildHuggingFaceUrl(model, file);
            var localPath = Path.Combine(modelPath, file);

            // Create subdirectories if needed
            var fileDir = Path.GetDirectoryName(localPath);
            if (fileDir != null)
            {
                Directory.CreateDirectory(fileDir);
            }

            OnStatusChanged($"Downloading {file}...");

            // Wrapper for progress tracking
            var fileDownloadedBytes = 0L;
            await DownloadFileAsync(
                url,
                localPath,
                (args) =>
                {
                    // Update total progress
                    var adjustedDownloaded = downloadedBytes + args.BytesDownloaded - fileDownloadedBytes;
                    fileDownloadedBytes = args.BytesDownloaded;

                    OnProgressChanged(new DownloadProgressEventArgs
                    {
                        BytesDownloaded = adjustedDownloaded,
                        TotalBytes = totalBytes,
                        ProgressPercentage = totalBytes > 0 ? (adjustedDownloaded * 100.0) / totalBytes : 0,
                        SpeedMBps = args.SpeedMBps
                    });
                },
                cancellationToken);

            downloadedBytes += fileSize;
        }

        // Verify
        OnStatusChanged("Verifying files...");
        if (!ModelVerifier.VerifyModel(modelPath, model))
        {
            throw new InvalidOperationException($"Model verification failed: {model.Id}");
        }
        OnStatusChanged("Verification complete");

        return modelPath;
    }

    private async Task DownloadFileAsync(string url, string outputPath, CancellationToken cancellationToken)
    {
        await DownloadFileAsync(url, outputPath, null, cancellationToken);
    }

    private async Task DownloadFileAsync(
        string url,
        string outputPath,
        Action<DownloadProgressEventArgs>? onProgress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var buffer = new byte[8192];
        long totalBytesRead = 0;

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                break;

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            if (stopwatch.ElapsedMilliseconds >= 500)
            {
                var progress = totalBytes > 0 ? (double)totalBytesRead / totalBytes : 0;
                var speedMBps = totalBytesRead / (1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;

                var args = new DownloadProgressEventArgs
                {
                    BytesDownloaded = totalBytesRead,
                    TotalBytes = totalBytes,
                    ProgressPercentage = progress * 100,
                    SpeedMBps = speedMBps
                };

                onProgress?.Invoke(args);
                OnProgressChanged(args);

                stopwatch.Restart();
            }
        }

        var finalArgs = new DownloadProgressEventArgs
        {
            BytesDownloaded = totalBytesRead,
            TotalBytes = totalBytes,
            ProgressPercentage = 100,
            SpeedMBps = 0
        };

        onProgress?.Invoke(finalArgs);
        OnProgressChanged(finalArgs);
    }

    private async Task ExtractArchiveAsync(string archivePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xjf \"{archivePath}\" -C \"{outputDirectory}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start tar process");
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"tar extraction failed: {error}");
        }
    }

    private async Task<long> GetFileSizeAsync(QwenModelDefinition model, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var url = BuildHuggingFaceUrl(model, filePath);
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return response.Content.Headers.ContentLength ?? 0;
            }
        }
        catch
        {
            // Fallback: assume estimated size if HEAD request fails
        }

        return 0;
    }

    private string BuildHuggingFaceUrl(QwenModelDefinition model, string filePath)
    {
        // https://huggingface.co/{repo}/resolve/main/{filepath}
        return $"https://huggingface.co/{model.RepositoryPath}/resolve/main/{filePath}";
    }

    private void OnProgressChanged(DownloadProgressEventArgs e) => ProgressChanged?.Invoke(this, e);
    private void OnStatusChanged(string status) => StatusChanged?.Invoke(this, status);
}

public class DownloadProgressEventArgs : EventArgs
{
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }
    public double ProgressPercentage { get; init; }
    public double SpeedMBps { get; init; }
}
