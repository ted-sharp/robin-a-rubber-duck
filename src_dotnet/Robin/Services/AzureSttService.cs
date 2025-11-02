using Android.Content;
using Android.Util;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Robin.Models;
using System.Text.Json;

namespace Robin.Services;

public class AzureSttService : IDisposable
{
    private const string TAG = "AzureSttService";
    private readonly Context _context;
    private SpeechRecognizer? _recognizer;
    private bool _isListening;
    private AzureSttConfig? _config;

    public event EventHandler<string>? RecognitionResult;
    public event EventHandler<string>? RecognitionError;
    public event EventHandler? RecognitionStarted;
    public event EventHandler? RecognitionStopped;

    public bool IsListening => _isListening;

    public AzureSttService(Context context)
    {
        _context = context;
    }

    /// <summary>
    /// Initialize Azure STT with configuration file
    /// </summary>
    public async Task<bool> InitializeAsync(string configFilePath)
    {
        try
        {
            Log.Info(TAG, $"Azure STT設定ファイル読込: {configFilePath}");

            if (!File.Exists(configFilePath))
            {
                Log.Error(TAG, $"設定ファイルが見つかりません: {configFilePath}");
                return false;
            }

            var configJson = await File.ReadAllTextAsync(configFilePath);
            _config = JsonSerializer.Deserialize(configJson, Robin.Models.RobinJsonContext.Default.AzureSttConfig);

            if (_config == null)
            {
                Log.Error(TAG, "設定ファイルの解析に失敗しました");
                return false;
            }

            Log.Info(TAG, $"Azure STT設定: Region={_config.Region}, Language={_config.Language}");

            // Test connection by creating a temporary recognizer
            var speechConfig = SpeechConfig.FromSubscription(_config.SubscriptionKey, _config.Region);
            speechConfig.SpeechRecognitionLanguage = _config.Language;

            if (!string.IsNullOrEmpty(_config.EndpointId))
            {
                speechConfig.EndpointId = _config.EndpointId;
                Log.Info(TAG, $"カスタムエンドポイント使用: {_config.EndpointId}");
            }

            Log.Info(TAG, "Azure STT初期化成功");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Azure STT初期化失敗: {ex.Message}");
            return false;
        }
    }

    public async Task StartListeningAsync()
    {
        if (_isListening)
        {
            Log.Warn(TAG, "既に録音中です");
            return;
        }

        if (_config == null)
        {
            Log.Error(TAG, "Azure STTが初期化されていません");
            RecognitionError?.Invoke(this, "Azure STTが初期化されていません");
            return;
        }

        try
        {
            Log.Info(TAG, "Azure STT音声認識開始");

            var speechConfig = SpeechConfig.FromSubscription(_config.SubscriptionKey, _config.Region);
            speechConfig.SpeechRecognitionLanguage = _config.Language;

            if (!string.IsNullOrEmpty(_config.EndpointId))
            {
                speechConfig.EndpointId = _config.EndpointId;
            }

            if (_config.EnableDictation)
            {
                speechConfig.EnableDictation();
            }

            if (_config.EnableProfanityFilter)
            {
                speechConfig.SetProfanity(ProfanityOption.Masked);
            }
            else
            {
                speechConfig.SetProfanity(ProfanityOption.Raw);
            }

            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            _recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            // Event handlers
            _recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
                {
                    Log.Info(TAG, $"認識成功: {e.Result.Text}");
                    RecognitionResult?.Invoke(this, e.Result.Text);
                }
            };

            _recognizer.Canceled += (s, e) =>
            {
                Log.Warn(TAG, $"認識キャンセル: {e.Reason}");
                if (e.Reason == CancellationReason.Error)
                {
                    Log.Error(TAG, $"エラーコード: {e.ErrorCode}, 詳細: {e.ErrorDetails}");
                    RecognitionError?.Invoke(this, $"Azure STTエラー: {e.ErrorDetails}");
                }
                _isListening = false;
                RecognitionStopped?.Invoke(this, EventArgs.Empty);
            };

            _recognizer.SessionStarted += (s, e) =>
            {
                Log.Info(TAG, "Azure STTセッション開始");
                _isListening = true;
                RecognitionStarted?.Invoke(this, EventArgs.Empty);
            };

            _recognizer.SessionStopped += (s, e) =>
            {
                Log.Info(TAG, "Azure STTセッション停止");
                _isListening = false;
                RecognitionStopped?.Invoke(this, EventArgs.Empty);
            };

            // Start continuous recognition
            await _recognizer.StartContinuousRecognitionAsync();
            Log.Info(TAG, "Azure STT継続認識開始");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Azure STT開始失敗: {ex.Message}");
            RecognitionError?.Invoke(this, $"Azure STT開始失敗: {ex.Message}");
            _isListening = false;
        }
    }

    public async Task StopListeningAsync()
    {
        if (!_isListening || _recognizer == null)
        {
            Log.Debug(TAG, "現在、録音中ではありません");
            return;
        }

        try
        {
            Log.Info(TAG, "Azure STT音声認識停止");
            await _recognizer.StopContinuousRecognitionAsync();
            _isListening = false;
            RecognitionStopped?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Azure STT停止失敗: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _recognizer?.Dispose();
        _recognizer = null;
    }
}
