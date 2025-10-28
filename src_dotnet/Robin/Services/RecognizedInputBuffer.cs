using Android.Util;

namespace Robin.Services;

/// <summary>
/// 音声認識結果のバッファリングおよびウォッチドッグ機能を管理
/// </summary>
public class RecognizedInputBuffer : IDisposable
{
    private readonly object _lockObj = new();
    private string _buffer = string.Empty;
    private DateTime _lastInputTime = DateTime.MinValue;
    private Timer? _watchdogTimer;

    /// <summary>
    /// ウォッチドッグのタイムアウト時間（ミリ秒）
    /// デフォルト: 2秒
    /// </summary>
    private int _timeoutMs = 2000;

    /// <summary>
    /// バッファが処理可能状態になった時のイベント
    /// </summary>
    public event EventHandler<string>? BufferReady;

    /// <summary>
    /// バッファが初期化された時のイベント
    /// </summary>
    public event EventHandler<string>? BufferUpdated;

    /// <summary>
    /// バッファが明示的にクリアされた時のイベント
    /// </summary>
    public event EventHandler? BufferCleared;

    public RecognizedInputBuffer(int timeoutMs = 2000)
    {
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// 認識結果をバッファに追加
    /// </summary>
    public void AddRecognition(string recognizedText)
    {
        lock (_lockObj)
        {
            _buffer += recognizedText;
            _lastInputTime = DateTime.Now;

            // ウォッチドッグをリセット
            ResetWatchdog();

            Log.Debug("RecognizedInputBuffer", $"バッファ更新: {_buffer}");
            BufferUpdated?.Invoke(this, _buffer);
        }
    }

    /// <summary>
    /// バッファの現在の内容を取得
    /// </summary>
    public string GetBuffer()
    {
        lock (_lockObj)
        {
            return _buffer;
        }
    }

    /// <summary>
    /// バッファをクリアして内容を返す
    /// </summary>
    public string FlushBuffer()
    {
        lock (_lockObj)
        {
            var result = _buffer;
            _buffer = string.Empty;
            _lastInputTime = DateTime.MinValue;

            // ウォッチドッグを停止
            StopWatchdog();

            Log.Debug("RecognizedInputBuffer", $"バッファフラッシュ: {result}");
            BufferCleared?.Invoke(this, EventArgs.Empty);

            return result;
        }
    }

    /// <summary>
    /// ウォッチドッグをリセット
    /// </summary>
    private void ResetWatchdog()
    {
        StopWatchdog();

        _watchdogTimer = new Timer(_ =>
        {
            lock (_lockObj)
            {
                if (!string.IsNullOrEmpty(_buffer))
                {
                    var content = _buffer;
                    Log.Info("RecognizedInputBuffer", $"ウォッチドッグ発火: {content}");
                    BufferReady?.Invoke(this, content);

                    // 処理されたかどうかはこのイベントハンドラーに任せる
                    // 自動的にはバッファはクリアしない（明示的な処理を待つ）
                }
            }
        }, null, _timeoutMs, Timeout.Infinite);
    }

    /// <summary>
    /// ウォッチドッグを停止
    /// </summary>
    private void StopWatchdog()
    {
        _watchdogTimer?.Dispose();
        _watchdogTimer = null;
    }

    /// <summary>
    /// バッファが空かどうか
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lockObj)
            {
                return string.IsNullOrEmpty(_buffer);
            }
        }
    }

    public void Dispose()
    {
        StopWatchdog();
    }
}
