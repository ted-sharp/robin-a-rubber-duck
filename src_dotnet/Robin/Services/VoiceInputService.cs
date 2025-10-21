using Android.Content;
using Android.Speech;
using Android.OS;

namespace Robin.Services;

public class VoiceInputService
{
    private readonly Context _context;
    private SpeechRecognizer? _speechRecognizer;
    private bool _isListening;

    public event EventHandler<string>? RecognitionResult;
    public event EventHandler<string>? RecognitionError;
    public event EventHandler? RecognitionStarted;
    public event EventHandler? RecognitionStopped;

    public bool IsListening => _isListening;

    public VoiceInputService(Context context)
    {
        _context = context;
    }

    public void StartListening()
    {
        if (_isListening)
            return;

        _speechRecognizer = SpeechRecognizer.CreateSpeechRecognizer(_context);
        if (_speechRecognizer == null)
        {
            RecognitionError?.Invoke(this, "音声認識サービスを利用できません");
            return;
        }

        _speechRecognizer.SetRecognitionListener(new RecognitionListener(this));

        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, "ja-JP");
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);

        _speechRecognizer.StartListening(intent);
        _isListening = true;
        RecognitionStarted?.Invoke(this, EventArgs.Empty);
    }

    public void StopListening()
    {
        if (!_isListening)
            return;

        _speechRecognizer?.StopListening();
        _isListening = false;
        RecognitionStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _speechRecognizer?.Destroy();
        _speechRecognizer?.Dispose();
    }

    private class RecognitionListener : Java.Lang.Object, IRecognitionListener
    {
        private readonly VoiceInputService _service;

        public RecognitionListener(VoiceInputService service)
        {
            _service = service;
        }

        public void OnBeginningOfSpeech()
        {
        }

        public void OnBufferReceived(byte[]? buffer)
        {
        }

        public void OnEndOfSpeech()
        {
            _service._isListening = false;
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
            _service.RecognitionError?.Invoke(_service, errorMessage);
        }

        public void OnEvent(int eventType, Bundle? @params)
        {
        }

        public void OnPartialResults(Bundle? partialResults)
        {
        }

        public void OnReadyForSpeech(Bundle? @params)
        {
        }

        public void OnResults(Bundle? results)
        {
            if (results == null)
                return;

            var matches = results.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            if (matches != null && matches.Count > 0)
            {
                var recognizedText = matches[0];
                if (!string.IsNullOrEmpty(recognizedText))
                {
                    _service.RecognitionResult?.Invoke(_service, recognizedText);
                }
            }

            _service._isListening = false;
            _service.RecognitionStopped?.Invoke(_service, EventArgs.Empty);
        }

        public void OnRmsChanged(float rmsdB)
        {
        }
    }
}
