using Android.Content;
using Android.Media;
using Android.Util;
using System.Net.Http;
using System.Text.Json;

namespace Robin.Services;

/// <summary>
/// LAN内のFaster Whisperサーバーに接続する音声認識サービス
/// </summary>
public class FasterWhisperService : IDisposable
{
    private const string TAG = "FasterWhisperService";
    private const int SampleRate = 16000;
    private const int BufferSizeInMs = 1000; // 1秒ごとに送信
    private const ChannelIn ChannelConfig = ChannelIn.Mono;
    private const Encoding AudioFormatEncoding = Encoding.Pcm16bit;

    private readonly Context _context;
    private readonly HttpClient _httpClient;
    private AudioRecord? _audioRecord;
    private bool _isListening;
    private bool _isDisposed;
    private Task? _audioProcessingTask;
    private CancellationTokenSource? _cancellationTokenSource;

    private string? _serverUrl;
    private string _language = "ja";

    public event EventHandler<string>? RecognitionResult;
    public event EventHandler<string>? RecognitionError;
    public event EventHandler? RecognitionStarted;
    public event EventHandler? RecognitionStopped;

    public bool IsListening => _isListening;

    public FasterWhisperService(Context context)
    {
        _context = context;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>
    /// Faster Whisperサーバーへの接続を初期化
    /// </summary>
    /// <param name="serverUrl">サーバーURL（例: http://192.168.1.100:8000）</param>
    /// <param name="language">認識言語（ja, en, zh, koなど）</param>
    public Task<bool> InitializeAsync(string serverUrl, string language = "ja")
    {
        try
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _language = language;

            Log.Info(TAG, $"Faster Whisper初期化: {_serverUrl}, 言語={_language}");

            // サーバー接続テスト（オプション）
            // 実際のAPIエンドポイントに合わせて調整
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Faster Whisper初期化失敗: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public void StartListening()
    {
        if (_isListening)
        {
            Log.Warn(TAG, "既に録音中です");
            return;
        }

        if (string.IsNullOrEmpty(_serverUrl))
        {
            Log.Error(TAG, "Faster Whisperサーバーが設定されていません");
            RecognitionError?.Invoke(this, "Faster Whisperサーバーが設定されていません");
            return;
        }

        try
        {
            Log.Info(TAG, "Faster Whisper音声認識開始");

            // AudioRecordのバッファサイズを計算
            int bufferSize = AudioRecord.GetMinBufferSize(SampleRate, ChannelConfig, AudioFormatEncoding);
            if (bufferSize <= 0)
            {
                throw new InvalidOperationException("バッファサイズの取得に失敗しました");
            }

            // 1秒分のバッファサイズを確保（16kHz * 2 bytes/sample * 1 second）
            int customBufferSize = SampleRate * 2 * (BufferSizeInMs / 1000);
            bufferSize = Math.Max(bufferSize, customBufferSize);

            _audioRecord = new AudioRecord(
                AudioSource.Mic,
                SampleRate,
                ChannelConfig,
                AudioFormatEncoding,
                bufferSize
            );

            if (_audioRecord.State != State.Initialized)
            {
                throw new InvalidOperationException("AudioRecordの初期化に失敗しました");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _audioRecord.StartRecording();
            _isListening = true;

            RecognitionStarted?.Invoke(this, EventArgs.Empty);

            // 音声データ収集と送信を開始
            _audioProcessingTask = Task.Run(() => ProcessAudioAsync(_cancellationTokenSource.Token));

            Log.Info(TAG, "Faster Whisper録音開始");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"録音開始エラー: {ex.Message}");
            RecognitionError?.Invoke(this, $"録音開始エラー: {ex.Message}");
            CleanupAudioRecord();
        }
    }

    public async Task StopListeningAsync()
    {
        if (!_isListening)
        {
            Log.Debug(TAG, "現在、録音中ではありません");
            return;
        }

        Log.Info(TAG, "Faster Whisper音声認識停止");

        _isListening = false;

        // キャンセル通知
        _cancellationTokenSource?.Cancel();

        // 処理完了を待つ
        if (_audioProcessingTask != null)
        {
            try
            {
                await _audioProcessingTask;
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"音声処理タスク終了エラー: {ex.Message}");
            }
        }

        CleanupAudioRecord();

        RecognitionStopped?.Invoke(this, EventArgs.Empty);
        Log.Info(TAG, "Faster Whisper停止完了");
    }

    private async Task ProcessAudioAsync(CancellationToken cancellationToken)
    {
        const int chunkDurationMs = 3000; // 3秒ごとに認識
        int chunkSize = SampleRate * 2 * (chunkDurationMs / 1000); // 16kHz * 2 bytes * 3 sec
        byte[] audioBuffer = new byte[chunkSize];
        int currentPosition = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested && _audioRecord != null)
            {
                // 音声データを読み取る
                int readSize = _audioRecord.Read(audioBuffer, currentPosition, chunkSize - currentPosition);

                if (readSize > 0)
                {
                    currentPosition += readSize;

                    // チャンクが満たされたら認識リクエストを送信
                    if (currentPosition >= chunkSize)
                    {
                        byte[] chunkToSend = new byte[currentPosition];
                        Array.Copy(audioBuffer, chunkToSend, currentPosition);

                        // サーバーに送信して認識
                        await RecognizeAudioChunkAsync(chunkToSend);

                        // バッファをリセット
                        currentPosition = 0;
                    }
                }
                else
                {
                    await Task.Delay(50, cancellationToken); // 待機
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Info(TAG, "音声処理がキャンセルされました");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"音声処理エラー: {ex.Message}");
            RecognitionError?.Invoke(this, $"音声処理エラー: {ex.Message}");
        }
    }

    private async Task RecognizeAudioChunkAsync(byte[] audioData)
    {
        try
        {
            // WAV形式に変換してサーバーに送信
            byte[] wavData = ConvertToWav(audioData, SampleRate);

            using var content = new ByteArrayContent(wavData);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

            // Faster WhisperのAPIエンドポイント（サーバー実装に合わせて調整）
            string endpoint = $"{_serverUrl}/v1/audio/transcriptions";

            using var formData = new MultipartFormDataContent();
            formData.Add(content, "file", "audio.wav");
            formData.Add(new StringContent("whisper-1"), "model");
            formData.Add(new StringContent(_language), "language");

            var response = await _httpClient.PostAsync(endpoint, formData);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FasterWhisperResponse>(responseBody);

            if (result?.Text != null && !string.IsNullOrWhiteSpace(result.Text))
            {
                Log.Info(TAG, $"認識結果: {result.Text}");
                RecognitionResult?.Invoke(this, result.Text);
            }
        }
        catch (HttpRequestException ex)
        {
            Log.Error(TAG, $"サーバー通信エラー: {ex.Message}");
            RecognitionError?.Invoke(this, $"サーバー通信エラー: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"認識エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// PCM音声データをWAV形式に変換
    /// </summary>
    private byte[] ConvertToWav(byte[] pcmData, int sampleRate)
    {
        int numChannels = 1; // モノラル
        int bitsPerSample = 16;
        int byteRate = sampleRate * numChannels * bitsPerSample / 8;
        int blockAlign = numChannels * bitsPerSample / 8;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFFヘッダー
        writer.Write(new char[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + pcmData.Length); // チャンクサイズ
        writer.Write(new char[] { 'W', 'A', 'V', 'E' });

        // fmtチャンク
        writer.Write(new char[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // サブチャンクサイズ
        writer.Write((short)1); // オーディオフォーマット（PCM）
        writer.Write((short)numChannels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // dataチャンク
        writer.Write(new char[] { 'd', 'a', 't', 'a' });
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return ms.ToArray();
    }

    private void CleanupAudioRecord()
    {
        if (_audioRecord != null)
        {
            try
            {
                if (_audioRecord.RecordingState == RecordState.Recording)
                {
                    _audioRecord.Stop();
                }
                _audioRecord.Release();
                _audioRecord.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"AudioRecordクリーンアップエラー: {ex.Message}");
            }
            _audioRecord = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Log.Info(TAG, "FasterWhisperService破棄");

        if (_isListening)
        {
            StopListeningAsync().Wait();
        }

        CleanupAudioRecord();
        _httpClient?.Dispose();

        _isDisposed = true;
    }

    /// <summary>
    /// Faster Whisper APIレスポンス
    /// </summary>
    private class FasterWhisperResponse
    {
        public string? Text { get; set; }
    }
}
