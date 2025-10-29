using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Util;

namespace Robin.Services
{
    /// <summary>
    /// モデルダウンロード管理サービス
    /// Qwen 2.5 1.5B ONNX int4、その他のONNXモデルの非同期ダウンロード
    /// </summary>
    public class ModelDownloadService
    {
        private static readonly string TAG = "ModelDownloadService";

        // Hugging Face CDN経由のダウンロードURL
        public const string HF_CDN_BASE = "https://huggingface.co";

        // サポートモデル定義
        public static class ModelDefinitions
        {
            // Qwen 2.5 1.5B Instruct ONNX (int4推奨)
            // 注: 実際のURLはHugging Faceの最新リリースに合わせて更新してください
            public static readonly ModelInfo Qwen25_1_5B_Int4 = new ModelInfo
            {
                ModelId = "qwen25-1.5b-int4",
                DisplayName = "Qwen 2.5 1.5B (ONNX int4)",
                RepositoryPath = "onnx-community/Qwen2.5-1.5B",
                LocalPath = "qwen25-1.5b-int4",
                RequiredFiles = new[]
                {
                    "onnx/model_quantized.onnx",      // 量子化済みモデル
                    "onnx/config.json",                // モデル設定
                    "tokenizer.json",                  // トークナイザー
                    "special_tokens_map.json"          // 特殊トークン定義
                },
                EstimatedSizeMB = 800,  // int4量子化版の推定サイズ
                ChecksumFile = "checksums.txt"  // 整合性検証用
            };

            // Qwen2.5用の追加設定
            public static readonly ModelInfo[] SupportedModels = new[]
            {
                Qwen25_1_5B_Int4
            };
        }

        private readonly Context _context;
        private readonly HttpClient _httpClient;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;
        public event EventHandler<DownloadCompleteEventArgs>? DownloadComplete;
        public event EventHandler<DownloadErrorEventArgs>? DownloadError;

        public ModelDownloadService(Context context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)  // 大容量ダウンロード用
            };
        }

        /// <summary>
        /// モデルが既にダウンロード済みか確認
        /// </summary>
        public bool IsModelDownloaded(ModelInfo model)
        {
            try
            {
                var modelDir = GetModelDirectory(model);
                if (!Directory.Exists(modelDir))
                    return false;

                // 必要なファイルがすべて存在するか確認
                return model.RequiredFiles.All(file =>
                    File.Exists(Path.Combine(modelDir, file)));
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"IsModelDownloaded check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// モデルを非同期でダウンロード
        /// </summary>
        public async Task<bool> DownloadModelAsync(
            ModelInfo model,
            IProgress<DownloadProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var linkedToken = _cancellationTokenSource.Token;

            try
            {
                // ダウンロード済みか確認
                if (IsModelDownloaded(model))
                {
                    Log.Info(TAG, $"Model already downloaded: {model.ModelId}");
                    OnDownloadComplete(new DownloadCompleteEventArgs
                    {
                        ModelId = model.ModelId,
                        LocalPath = GetModelDirectory(model),
                        Success = true
                    });
                    return true;
                }

                Log.Info(TAG, $"Starting download: {model.ModelId}");

                var modelDir = GetModelDirectory(model);
                Directory.CreateDirectory(modelDir);

                // 各ファイルをダウンロード
                long totalBytes = 0;
                long downloadedBytes = 0;

                foreach (var file in model.RequiredFiles)
                {
                    linkedToken.ThrowIfCancellationRequested();

                    var fileSize = await GetFileSizeAsync(model, file);
                    totalBytes += fileSize;
                }

                foreach (var file in model.RequiredFiles)
                {
                    linkedToken.ThrowIfCancellationRequested();

                    var result = await DownloadFileAsync(
                        model,
                        file,
                        modelDir,
                        progress,
                        downloadedBytes,
                        totalBytes);

                    if (!result.Success)
                        return false;

                    downloadedBytes = result.BytesDownloaded;
                }

                Log.Info(TAG, $"Download completed: {model.ModelId}");
                OnDownloadComplete(new DownloadCompleteEventArgs
                {
                    ModelId = model.ModelId,
                    LocalPath = modelDir,
                    Success = true
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                Log.Warn(TAG, $"Download cancelled: {model.ModelId}");
                OnDownloadError(new DownloadErrorEventArgs
                {
                    ModelId = model.ModelId,
                    Error = "Download cancelled by user"
                });
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Download failed: {ex.Message}\n{ex.StackTrace}");
                OnDownloadError(new DownloadErrorEventArgs
                {
                    ModelId = model.ModelId,
                    Error = ex.Message
                });
                return false;
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// ダウンロード中止
        /// </summary>
        public void CancelDownload()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task<FileDownloadResult> DownloadFileAsync(
            ModelInfo model,
            string filePath,
            string modelDir,
            IProgress<DownloadProgressEventArgs>? progress,
            long downloadedBytesBefore,
            long totalBytes)
        {
            try
            {
                var url = BuildDownloadUrl(model, filePath);
                var localPath = Path.Combine(modelDir, filePath);

                // ローカルディレクトリ作成
                var fileDir = Path.GetDirectoryName(localPath);
                if (fileDir != null)
                    Directory.CreateDirectory(fileDir);

                Log.Debug(TAG, $"Downloading: {filePath}");

                using (var response = await _httpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    _cancellationTokenSource?.Token ?? default))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error(TAG, $"HTTP error {response.StatusCode}: {filePath}");
                        return new FileDownloadResult { Success = false, BytesDownloaded = downloadedBytesBefore };
                    }

                    long downloadedBytesTotal = downloadedBytesBefore;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(
                        localPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        8192,
                        useAsync: true))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(
                            buffer,
                            0,
                            buffer.Length,
                            _cancellationTokenSource?.Token ?? default)) != 0)
                        {
                            await fileStream.WriteAsync(
                                buffer,
                                0,
                                bytesRead,
                                _cancellationTokenSource?.Token ?? default);

                            downloadedBytesTotal += bytesRead;

                            progress?.Report(new DownloadProgressEventArgs
                            {
                                ModelId = model.ModelId,
                                CurrentFile = filePath,
                                BytesDownloaded = downloadedBytesTotal,
                                TotalBytes = totalBytes,
                                ProgressPercentage = totalBytes > 0
                                    ? (int)((downloadedBytesTotal * 100L) / totalBytes)
                                    : 0
                            });
                        }
                    }

                    Log.Debug(TAG, $"File downloaded: {filePath}");
                    return new FileDownloadResult { Success = true, BytesDownloaded = downloadedBytesTotal };
                }
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"File download error ({filePath}): {ex.Message}");
                return new FileDownloadResult { Success = false, BytesDownloaded = downloadedBytesBefore };
            }
        }

        private async Task<long> GetFileSizeAsync(ModelInfo model, string filePath)
        {
            try
            {
                var url = BuildDownloadUrl(model, filePath);
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                using (var response = await _httpClient.SendAsync(request))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return response.Content.Headers.ContentLength ?? 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"Failed to get file size: {ex.Message}");
            }

            return 0;
        }

        private string BuildDownloadUrl(ModelInfo model, string filePath)
        {
            // Hugging Face resolve/main パターン
            // https://huggingface.co/{repo}/resolve/main/{filepath}
            return $"{HF_CDN_BASE}/{model.RepositoryPath}/resolve/main/{filePath}";
        }

        private string GetModelDirectory(ModelInfo model)
        {
            var cacheDir = _context.CacheDir?.AbsolutePath ?? _context.FilesDir?.AbsolutePath;
            return Path.Combine(cacheDir ?? "/data/local/tmp", "models", model.LocalPath);
        }

        protected virtual void OnDownloadProgress(DownloadProgressEventArgs e)
        {
            DownloadProgress?.Invoke(this, e);
        }

        protected virtual void OnDownloadComplete(DownloadCompleteEventArgs e)
        {
            DownloadComplete?.Invoke(this, e);
        }

        protected virtual void OnDownloadError(DownloadErrorEventArgs e)
        {
            DownloadError?.Invoke(this, e);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// モデル定義
    /// </summary>
    public class ModelInfo
    {
        public string ModelId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string RepositoryPath { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty;
        public string[] RequiredFiles { get; set; } = Array.Empty<string>();
        public int EstimatedSizeMB { get; set; }
        public string? ChecksumFile { get; set; }
    }

    /// <summary>
    /// ダウンロード進捗イベント
    /// </summary>
    public class DownloadProgressEventArgs : EventArgs
    {
        public string ModelId { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public int ProgressPercentage { get; set; }
    }

    /// <summary>
    /// ダウンロード完了イベント
    /// </summary>
    public class DownloadCompleteEventArgs : EventArgs
    {
        public string ModelId { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty;
        public bool Success { get; set; }
    }

    /// <summary>
    /// ダウンロードエラーイベント
    /// </summary>
    public class DownloadErrorEventArgs : EventArgs
    {
        public string ModelId { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// ファイルダウンロード結果
    /// </summary>
    internal class FileDownloadResult
    {
        public bool Success { get; set; }
        public long BytesDownloaded { get; set; }
    }
}
