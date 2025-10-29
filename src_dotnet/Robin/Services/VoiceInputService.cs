using Android.Content;
using Android.OS;
using Android.Speech;
using Android.Util;

namespace Robin.Services;

public class VoiceInputService
{
    private const string TAG = "VoiceInputService";
    private readonly Context _context;
    private SpeechRecognizer? _speechRecognizer;
    private bool _isListening;
    private bool _continuousMode;

    public event EventHandler<string>? RecognitionResult;
    public event EventHandler<string>? RecognitionError;
    public event EventHandler? RecognitionStarted;
    public event EventHandler? RecognitionStopped;

    public bool IsListening => _isListening;
    public bool IsContinuousMode => _continuousMode;

    public VoiceInputService(Context context)
    {
        _context = context;
    }

    public void StartListening(bool enableContinuousMode = false)
    {
        if (enableContinuousMode)
        {
            _continuousMode = true;
            Log.Info(TAG, "🔄 継続モードを有効化");
        }

        if (_isListening)
        {
            Log.Warn(TAG, "⚠️ 既に録音中です");
            return;
        }

        Log.Info(TAG, "🎙️ 音声認識開始 (Android標準エンジン, 言語=ja-JP)");

        // 既存のインスタンスを確実に破棄
        if (_speechRecognizer != null)
        {
            try
            {
                _speechRecognizer.Destroy();
                _speechRecognizer.Dispose();
                Log.Debug(TAG, "前回のSpeechRecognizerを破棄");
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"破棄時のエラー: {ex.Message}");
            }
            _speechRecognizer = null;
        }

        _speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(_context);
        if (_speechRecognizer == null)
        {
            string errorMsg = "❌ 音声認識サービスを利用できません";
            Log.Error(TAG, errorMsg);
            RecognitionError?.Invoke(this, errorMsg);
            return;
        }

        _speechRecognizer.SetRecognitionListener(new RecognitionListener(this));

        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, "ja-JP");
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);

        // 継続モードの場合、無音タイムアウトを大幅に延長して途切れにくくする
        if (_continuousMode)
        {
            intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, 30000L); // 30秒の無音で終了
            intent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, 30000L); // 30秒
            intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 30000L); // 最小発話時間を30秒に
            Log.Debug(TAG, "継続モード設定: 無音タイムアウト=30秒");
        }

        _speechRecognizer.StartListening(intent);
        _isListening = true;
        Log.Info(TAG, "✓ SpeechRecognizer開始");
        RecognitionStarted?.Invoke(this, EventArgs.Empty);
    }

    public void StopListening()
    {
        _continuousMode = false;

        if (!_isListening)
        {
            Log.Debug(TAG, "⚠️ 現在、録音中ではありません");
            return;
        }

        Log.Info(TAG, "⏹️ 音声認識停止");
        _speechRecognizer?.StopListening();
        _isListening = false;
        RecognitionStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _speechRecognizer?.Destroy();
        _speechRecognizer?.Dispose();
    }

    private sealed class RecognitionListener : Java.Lang.Object, IRecognitionListener
    {
        private readonly VoiceInputService _service;

        public RecognitionListener(VoiceInputService service)
        {
            _service = service;
        }

        public void OnBeginningOfSpeech()
        {
            Log.Debug(TAG, "🎤 音声検出 - 発話が始まりました");
        }

        public void OnBufferReceived(byte[]? buffer)
        {
            if (buffer != null)
            {
                Log.Debug(TAG, $"📦 バッファ受信: {buffer.Length} bytes");
            }
        }

        public void OnEndOfSpeech()
        {
            _service._isListening = false;
            Log.Info(TAG, "⏸️ 発話終了 - 認識待機中");
        }

        public void OnError(SpeechRecognizerError error)
        {
            _service._isListening = false;
            var errorMessage = error switch
            {
                SpeechRecognizerError.NoMatch => "音声が認識できませんでした",
                SpeechRecognizerError.Network => "ネットワークエラーが発生しました",
                SpeechRecognizerError.Audio => "マイクにアクセスできません",
                SpeechRecognizerError.InsufficientPermissions => "マイクの使用許可が必要です",
                _ => $"音声認識エラー: {error}"
            };

            Log.Error(TAG, $"❌ エラー ({error}): {errorMessage}");
            _service.RecognitionError?.Invoke(_service, errorMessage);

            // NoMatchエラー（無音タイムアウト）の場合は継続モードを終了
            if (error == SpeechRecognizerError.NoMatch)
            {
                _service._continuousMode = false;
                _service.RecognitionStopped?.Invoke(_service, EventArgs.Empty);
            }
        }

        public void OnEvent(int eventType, Bundle? @params)
        {
            Log.Debug(TAG, $"🔔 イベント: {eventType}");
        }

        public void OnPartialResults(Bundle? partialResults)
        {
            if (partialResults != null)
            {
                var partialMatches = partialResults.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
                if (partialMatches != null && partialMatches.Count > 0)
                {
                    var partialText = partialMatches[0];
                    if (!string.IsNullOrEmpty(partialText))
                    {
                        Log.Debug(TAG, $"🔤 途中結果: 「{partialText}」");
                    }
                }
            }
        }

        public void OnReadyForSpeech(Bundle? @params)
        {
            Log.Debug(TAG, "🟢 準備完了 - マイク入力待機中");
        }

        public void OnResults(Bundle? results)
        {
            if (results == null)
            {
                Log.Warn(TAG, "⚠️ 認識結果がnullです");
                return;
            }

            var matches = results.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            if (matches != null && matches.Count > 0)
            {
                var recognizedText = matches[0];
                if (!string.IsNullOrEmpty(recognizedText))
                {
                    Log.Info(TAG, $"✅ 認識成功: 「{recognizedText}」");
                    _service.RecognitionResult?.Invoke(_service, recognizedText);
                }
                else
                {
                    Log.Info(TAG, "⚠️ 認識結果は空です");
                }
            }
            else
            {
                Log.Info(TAG, "⚠️ 認識結果がありません");
            }

            _service._isListening = false;
            _service.RecognitionStopped?.Invoke(_service, EventArgs.Empty);

            // 継続モードの場合、自動的に再開
            if (_service._continuousMode)
            {
                Log.Debug(TAG, "🔄 継続モード - 300ms後に再開予定");
                // 少し待ってから再開（SpeechRecognizerの再初期化が必要なため）
                new Handler(Looper.MainLooper!).PostDelayed(() =>
                {
                    if (_service._continuousMode && !_service._isListening)
                    {
                        Log.Debug(TAG, "🔄 継続モード - 再開開始");
                        _service.StartListening();
                    }
                }, 300); // 300ms待機
            }
        }

        public void OnRmsChanged(float rmsdB)
        {
            Log.Debug(TAG, $"📊 RMS変化: {rmsdB:F2} dB");
        }
    }
}
