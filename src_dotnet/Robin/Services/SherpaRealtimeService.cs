using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Util;
using Com.K2fsa.Sherpa.Onnx;
using Java.IO;

namespace Robin.Services;

/// <summary>
/// Sherpa-ONNXを使用した音声認識サービス
/// すべてOffline(非ストリーミング)モデル・システム音なし
/// チャンク処理で疑似リアルタイム認識を実現
/// </summary>
public class SherpaRealtimeService : IDisposable
{
    private const string TAG = "SherpaRealtimeService";
    private readonly Context _context;
    private AudioRecord? _audioRecord;
    private Thread? _recordingThread;

    // ===== 認識状態 =====
    /// <summary>
    /// ユーザーがマイクボタンをONにしているか（マイクが有効かどうか）
    ///
    /// true の意味:
    ///   - ユーザーがマイクボタンをONにした
    ///   - 現在、音声入力が進行中（マイクから録音中）
    ///   - 認識完了後、自動で次の認識に進む（連続認識モード）
    ///
    /// false の意味:
    ///   - ユーザーがマイクボタンをOFFにした
    ///   - 音声入力が停止している
    ///   - 認識は実行されない
    ///
    /// 設定: StartListening() で true、StopListening() で false
    /// </summary>
    private bool _isListening;

    /// <summary>
    /// モデルが正常に初期化されたかどうか
    /// true: 初期化完了、認識可能 / false: 未初期化または初期化失敗
    ///
    /// 設定: InitializeAsync() で true、DisposeRecognizer() で false
    /// </summary>
    private bool _isInitialized;


    // ===== ログ機能 =====
    /// <summary>詳細ログ機能（画面から切り替え可能）</summary>
    public static bool VerboseLoggingEnabled { get; set; } = false;

    // ===== 音声処理パラメータ =====
    private const int SampleRate = 16000;
    private const ChannelIn ChannelConfig = ChannelIn.Mono;
    private const Encoding AudioFormat = Encoding.Pcm16bit;
    private const int BufferSizeInMs = 100; // 100ms バッファ
    private const int ChunkDurationSeconds = 3; // 3秒ごとに認識（精度優先）
    private const float ChunkOverlapRatio = 0.0f; // オーバーラップなし（前のデータが混在しないように）

    // ===== Sherpa-ONNX 認識エンジン =====
    private OfflineRecognizer? _recognizer;
    private OnlineRecognizer? _onlineRecognizer;
    private OnlineStream? _onlineStream;
    private bool _isStreamingModel = false; // true: ストリーミングモデル, false: オフラインモデル
    private readonly List<float> _audioBuffer = new List<float>();
    private readonly object _bufferLock = new object();
    private string _selectedLanguage = "ja"; // デフォルト言語は日本語

    // 認識ライフサイクル用イベント
    public event EventHandler<string>? FinalResult;
    public event EventHandler<string>? Error;
    public event EventHandler? RecognitionStarted;
    public event EventHandler? RecognitionStopped;
    public event EventHandler<InitializationProgressEventArgs>? InitializationProgress;

    public class InitializationProgressEventArgs : EventArgs
    {
        public string Status { get; set; } = "";
        public int ProgressPercentage { get; set; }
    }

    public bool IsListening => _isListening;
    public bool IsInitialized => _isInitialized;

    public SherpaRealtimeService(Context context)
    {
        _context = context;
    }

    /// <summary>
    /// Zipformerモデル用の設定を作成
    /// </summary>
    private OfflineRecognizerConfig CreateZipformerConfig(string pathPrefix)
    {
        string encoderFile = $"{pathPrefix}/encoder-epoch-99-avg-1.onnx";
        string decoderFile = $"{pathPrefix}/decoder-epoch-99-avg-1.onnx";
        string joinerFile = $"{pathPrefix}/joiner-epoch-99-avg-1.int8.onnx";
        string tokensFile = $"{pathPrefix}/tokens.txt";

        Log.Info(TAG, $"モデル設定 (Zipformer Transducer):");
        Log.Info(TAG, $"  - Encoder: {encoderFile}");
        Log.Info(TAG, $"  - Decoder: {decoderFile}");
        Log.Info(TAG, $"  - Joiner: {joinerFile}");
        Log.Info(TAG, $"  - Tokens: {tokensFile}");

        // 注: ファイル存在チェックは CheckModelFilesAsync で既に完了しているため、ここでは省略

        var transducerConfig = new OfflineTransducerModelConfig
        {
            Encoder = encoderFile,
            Decoder = decoderFile,
            Joiner = joinerFile
        };

        var modelConfig = new OfflineModelConfig();
        modelConfig.Transducer = transducerConfig;
        modelConfig.Tokens = tokensFile;
        modelConfig.NumThreads = 4;
        modelConfig.Debug = false;
        modelConfig.ModelType = "zipformer";

        var featConfig = new FeatureConfig
        {
            SampleRate = SampleRate,
            FeatureDim = 80
        };

        return new OfflineRecognizerConfig
        {
            FeatConfig = featConfig,
            ModelConfig = modelConfig,
            DecodingMethod = "modified_beam_search" // beam_searchに変更（精度向上）
        };
    }

    /// <summary>
    /// SenseVoiceモデル用の設定を作成 (Offline, 非ストリーミング)
    /// </summary>
    private OfflineRecognizerConfig CreateSenseVoiceConfig(string pathPrefix)
    {
        string modelFile = $"{pathPrefix}/model.int8.onnx";
        string tokensFile = $"{pathPrefix}/tokens.txt";

        Log.Info(TAG, $"モデル設定 (SenseVoice - Offline CTC):");
        Log.Info(TAG, $"  - Model: {modelFile}");
        Log.Info(TAG, $"  - Tokens: {tokensFile}");
        Log.Info(TAG, $"  - UseITN: 1 (有効)");
        Log.Info(TAG, $"  - Language: {_selectedLanguage}");

        // 注: ファイル存在チェックは CheckModelFilesAsync で既に完了しているため、ここでは省略

        var senseVoiceConfig = new OfflineSenseVoiceModelConfig
        {
            Model = modelFile,
            Language = _selectedLanguage, // 選択した言語を設定
            UseInverseTextNormalization = true // 数字のテキスト正規化などを有効化
        };

        var modelConfig = new OfflineModelConfig();
        modelConfig.SenseVoice = senseVoiceConfig;
        modelConfig.Tokens = tokensFile;
        modelConfig.NumThreads = 4;
        modelConfig.Debug = false;
        modelConfig.ModelType = "sense_voice";

        var featConfig = new FeatureConfig
        {
            SampleRate = SampleRate,
            FeatureDim = 80
        };

        return new OfflineRecognizerConfig
        {
            FeatConfig = featConfig,
            ModelConfig = modelConfig,
            DecodingMethod = "greedy_search" // SenseVoiceはgreedy_searchのみサポート
        };
    }

    /// <summary>
    /// Nemoモデル用の設定を作成 (Enc-Dec CTC)
    /// </summary>
    private OfflineRecognizerConfig CreateNemoConfig(string pathPrefix)
    {
        string modelFile = $"{pathPrefix}/model.int8.onnx";
        string tokensFile = $"{pathPrefix}/tokens.txt";

        Log.Info(TAG, $"モデル設定 (Nemo Enc-Dec CTC):");
        Log.Info(TAG, $"  - Model: {modelFile}");
        Log.Info(TAG, $"  - Tokens: {tokensFile}");

        // 注: ファイル存在チェックは CheckModelFilesAsync で既に完了しているため、ここでは省略

        var nemoConfig = new OfflineNemoEncDecCtcModelConfig(modelFile);

        var modelConfig = new OfflineModelConfig();
        modelConfig.Nemo = nemoConfig;
        modelConfig.Tokens = tokensFile;
        modelConfig.NumThreads = 2;
        modelConfig.Debug = false;
        modelConfig.ModelType = "nemo_ctc";

        var featConfig = new FeatureConfig
        {
            SampleRate = SampleRate,
            FeatureDim = 80
        };

        return new OfflineRecognizerConfig
        {
            FeatConfig = featConfig,
            ModelConfig = modelConfig,
            DecodingMethod = "greedy_search"
        };
    }

    /// <summary>
    /// ストリーミングZipformerモデル用の設定を作成 (Online, リアルタイムストリーミング)
    /// </summary>
    private OnlineRecognizerConfig CreateStreamingZipformerConfig(string pathPrefix)
    {
        string encoderFile = $"{pathPrefix}/encoder-epoch-75-avg-11-chunk-16-left-128.int8.onnx";
        string decoderFile = $"{pathPrefix}/decoder-epoch-75-avg-11-chunk-16-left-128.onnx";
        string joinerFile = $"{pathPrefix}/joiner-epoch-75-avg-11-chunk-16-left-128.int8.onnx";
        string tokensFile = $"{pathPrefix}/tokens.txt";

        Log.Info(TAG, $"モデル設定 (Streaming Zipformer Transducer):");
        Log.Info(TAG, $"  - Encoder: {encoderFile}");
        Log.Info(TAG, $"  - Decoder: {decoderFile}");
        Log.Info(TAG, $"  - Joiner: {joinerFile}");
        Log.Info(TAG, $"  - Tokens: {tokensFile}");

        var transducerConfig = new OnlineTransducerModelConfig
        {
            Encoder = encoderFile,
            Decoder = decoderFile,
            Joiner = joinerFile
        };

        var modelConfig = new OnlineModelConfig();
        modelConfig.Transducer = transducerConfig;
        modelConfig.Tokens = tokensFile;
        modelConfig.NumThreads = 4;
        modelConfig.Debug = false;
        modelConfig.ModelType = "zipformer2";

        var featConfig = new FeatureConfig
        {
            SampleRate = SampleRate,
            FeatureDim = 80
        };

        // エンドポイント設定（発話区切り検出）
        var endpointConfig = new EndpointConfig
        {
            Rule1 = new EndpointRule(false, 2.4f, 0.0f),
            Rule2 = new EndpointRule(true, 1.2f, 0.0f),
            Rule3 = new EndpointRule(false, 0.0f, 300.0f)
        };

        return new OnlineRecognizerConfig
        {
            FeatConfig = featConfig,
            ModelConfig = modelConfig,
            DecodingMethod = "greedy_search", // ストリーミングではgreedy_searchを推奨
            EnableEndpoint = true, // ストリーミングでは発話区切り検出を有効化
            EndpointConfig = endpointConfig
        };
    }

    /// <summary>
    /// Whisperモデル用の設定を作成 (Offline, 非ストリーミング)
    /// </summary>
    private OfflineRecognizerConfig CreateWhisperConfig(string pathPrefix)
    {
        string encoderFile = $"{pathPrefix}/tiny-encoder.int8.onnx";
        string decoderFile = $"{pathPrefix}/tiny-decoder.int8.onnx";
        string tokensFile = $"{pathPrefix}/tiny-tokens.txt";

        Log.Info(TAG, $"モデル設定 (Whisper - Offline):");
        Log.Info(TAG, $"  - Encoder: {encoderFile}");
        Log.Info(TAG, $"  - Decoder: {decoderFile}");
        Log.Info(TAG, $"  - Tokens: {tokensFile}");
        Log.Info(TAG, $"  - Language: {_selectedLanguage}");
        Log.Info(TAG, $"  - Task: transcribe");

        // 注: ファイル存在チェックは CheckModelFilesAsync で既に完了しているため、ここでは省略

        var whisperConfig = new OfflineWhisperModelConfig
        {
            Encoder = encoderFile,
            Decoder = decoderFile,
            Language = _selectedLanguage, // 選択した言語を設定（空文字列=自動検出）
            Task = "transcribe", // "transcribe" または "translate"
            TailPaddings = -1 // -1 = デフォルト値を使用
        };

        var modelConfig = new OfflineModelConfig();
        modelConfig.Whisper = whisperConfig;
        modelConfig.Tokens = tokensFile;
        modelConfig.NumThreads = 2;
        modelConfig.Debug = false;
        modelConfig.ModelType = "whisper";

        var featConfig = new FeatureConfig
        {
            SampleRate = SampleRate,
            FeatureDim = 80
        };

        return new OfflineRecognizerConfig
        {
            FeatConfig = featConfig,
            ModelConfig = modelConfig,
            DecodingMethod = "greedy_search"
        };
    }

    /// <summary>
    /// Sherpa-ONNX認識器を初期化
    /// </summary>
    /// <param name="modelPath">モデルパス（assetsパス or ファイルシステムパス）</param>
    /// <param name="isFilePath">trueの場合ファイルシステムパス、falseの場合assetsパス</param>
    /// <param name="language">認識言語（SenseVoice/Whisper用）。デフォルト: "ja"</param>
    public async Task<bool> InitializeAsync(string modelPath, bool isFilePath = false, string language = "ja")
    {
        try
        {
            Log.Info(TAG, $"初期化開始 - モデルパス: {modelPath} (FilePath={isFilePath}, Language={language})");

            // 選択言語を保存
            _selectedLanguage = language;

            // 古いバッファをクリア（前のモデルのデータが混在するのを防ぐ）
            lock (_bufferLock)
            {
                _audioBuffer.Clear();
                Log.Debug(TAG, "モデル初期化前：音声バッファをクリアしました");
            }

            // 既に実行中の音声認識を停止
            if (_isListening)
            {
                Log.Info(TAG, "既存の音声認識を停止します...");
                StopListening();
            }

            // 古いモデルと関連リソースを破棄
            DisposeRecognizer();

            InitializationProgress?.Invoke(this, new InitializationProgressEventArgs
            {
                Status = "モデルファイルを確認中...",
                ProgressPercentage = 10
            });

            // モデルファイルの存在確認
            if (!CheckModelFiles(modelPath, isFilePath))
            {
                string errorMsg = "モデルファイルが見つかりません";
                Log.Error(TAG, errorMsg);
                Error?.Invoke(this, errorMsg);
                return false;
            }

            Log.Info(TAG, "モデルファイル確認完了");

            InitializationProgress?.Invoke(this, new InitializationProgressEventArgs
            {
                Status = "音声認識モデルを読み込み中...",
                ProgressPercentage = 30
            });

            // モデル設定を作成（ファイル内容から判定）
            string pathPrefix = isFilePath ? modelPath : $"{modelPath}";

            // ファイル内容で判定
            string[] files;
            if (isFilePath)
            {
                files = Directory.GetFiles(modelPath).Select(Path.GetFileName).ToArray()!;
            }
            else
            {
                files = _context.Assets?.List(modelPath) ?? Array.Empty<string>();
            }

            // ファイルパターンでモデルタイプを判定
            bool hasEncoderDecoder = files.Any(f => f?.Contains("encoder") == true) &&
                                     files.Any(f => f?.Contains("decoder") == true);
            bool hasJoiner = files.Any(f => f?.Contains("joiner") == true);
            bool hasModelOnnx = files.Any(f => f?.Equals("model.int8.onnx", StringComparison.OrdinalIgnoreCase) == true);
            bool hasWhisperTokens = files.Any(f => f?.Equals("tiny-tokens.txt", StringComparison.OrdinalIgnoreCase) == true);
            bool hasStreamingEncoder = files.Any(f => f?.Contains("chunk-16-left-128") == true);

            // ストリーミングモデルの判定と初期化
            if (hasEncoderDecoder && hasJoiner && hasStreamingEncoder)
            {
                Log.Info(TAG, "モデルタイプ: Streaming Zipformer Transducer");
                _isStreamingModel = true;
                var onlineConfig = CreateStreamingZipformerConfig(pathPrefix);

                Log.Info(TAG, "OnlineRecognizer作成中...");
                _onlineRecognizer = new OnlineRecognizer(_context.Assets, onlineConfig);
                _onlineStream = _onlineRecognizer.CreateStream(""); // hotwordsパラメータは空文字列
                Log.Info(TAG, "OnlineRecognizer作成完了");
            }
            else if (hasEncoderDecoder && hasJoiner)
            {
                Log.Info(TAG, "モデルタイプ: Zipformer Transducer (Offline)");
                _isStreamingModel = false;
                OfflineRecognizerConfig config = CreateZipformerConfig(pathPrefix);
                _recognizer = new OfflineRecognizer(_context.Assets, config);
                Log.Info(TAG, "OfflineRecognizer作成完了");
            }
            else if (hasWhisperTokens && hasEncoderDecoder)
            {
                Log.Info(TAG, "モデルタイプ: Whisper (Offline)");
                _isStreamingModel = false;
                OfflineRecognizerConfig config = CreateWhisperConfig(pathPrefix);
                _recognizer = new OfflineRecognizer(_context.Assets, config);
                Log.Info(TAG, "OfflineRecognizer作成完了");
            }
            else if (hasModelOnnx && files.Any(f => f?.Equals("tokens.txt", StringComparison.OrdinalIgnoreCase) == true))
            {
                _isStreamingModel = false;
                OfflineRecognizerConfig config;

                // SenseVoiceまたはNemoの判定（モデル名から推測）
                if (modelPath.Contains("sense-voice", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info(TAG, "モデルタイプ: SenseVoice (Offline)");
                    config = CreateSenseVoiceConfig(pathPrefix);
                }
                else if (modelPath.Contains("nemo", StringComparison.OrdinalIgnoreCase) ||
                         modelPath.Contains("parakeet", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info(TAG, "モデルタイプ: Nemo CTC (Offline)");
                    config = CreateNemoConfig(pathPrefix);
                }
                else
                {
                    // デフォルトはSenseVoiceとして扱う
                    Log.Info(TAG, "モデルタイプ: model.int8.onnx - SenseVoiceとして扱う (Offline)");
                    config = CreateSenseVoiceConfig(pathPrefix);
                }

                _recognizer = new OfflineRecognizer(_context.Assets, config);
                Log.Info(TAG, "OfflineRecognizer作成完了");
            }
            else
            {
                Log.Info(TAG, "モデルタイプ: 判定不可、Zipformer (Offline) として扱う");
                _isStreamingModel = false;
                OfflineRecognizerConfig config = CreateZipformerConfig(pathPrefix);
                _recognizer = new OfflineRecognizer(_context.Assets, config);
                Log.Info(TAG, "OfflineRecognizer作成完了");
            }

            InitializationProgress?.Invoke(this, new InitializationProgressEventArgs
            {
                Status = "マイクを初期化中...",
                ProgressPercentage = 80
            });

            // AudioRecordの初期化
            Log.Info(TAG, "AudioRecord初期化中...");
            int bufferSize = AudioRecord.GetMinBufferSize(SampleRate, ChannelConfig, AudioFormat);
            if (bufferSize <= 0)
            {
                string errorMsg = "AudioRecordの初期化に失敗しました";
                Log.Error(TAG, errorMsg);
                Error?.Invoke(this, errorMsg);
                return false;
            }

            // バッファサイズを調整（100ms分）
            int desiredBufferSize = SampleRate * BufferSizeInMs / 1000 * 2; // 16bit = 2 bytes
            bufferSize = Math.Max(bufferSize, desiredBufferSize);

            Log.Info(TAG, $"AudioRecord設定: SampleRate={SampleRate}, BufferSize={bufferSize}");

            _audioRecord = new AudioRecord(
                AudioSource.Mic,
                SampleRate,
                ChannelConfig,
                AudioFormat,
                bufferSize
            );

            if (_audioRecord.State != State.Initialized)
            {
                string errorMsg = "AudioRecordの初期化に失敗しました";
                Log.Error(TAG, errorMsg);
                Error?.Invoke(this, errorMsg);
                return false;
            }

            _isInitialized = true;
            Log.Info(TAG, "初期化完了");

            InitializationProgress?.Invoke(this, new InitializationProgressEventArgs
            {
                Status = "初期化完了",
                ProgressPercentage = 100
            });

            return true;
        }
        catch (Exception ex)
        {
            string errorMsg = $"初期化エラー: {ex.Message}";
            Log.Error(TAG, errorMsg);
            Log.Error(TAG, $"StackTrace: {ex.StackTrace}");
            Error?.Invoke(this, errorMsg);

            InitializationProgress?.Invoke(this, new InitializationProgressEventArgs
            {
                Status = $"エラー: {ex.Message}",
                ProgressPercentage = 0
            });

            return false;
        }
    }

    /// <summary>
    /// リアルタイム音声認識を開始
    /// </summary>
    public void StartListening()
    {
        if (!_isInitialized)
        {
            string errorMsg = "❌ サービスが初期化されていません";
            Log.Error(TAG, errorMsg);
            Error?.Invoke(this, errorMsg);
            return;
        }

        if (_isListening)
        {
            Log.Warn(TAG, "⚠️ 既に録音中です");
            return;
        }

        string verboseFlag = VerboseLoggingEnabled ? "✓詳細ログON" : "";
        string modelType = _isStreamingModel ? "ストリーミング" : $"オフライン(チャンク={ChunkDurationSeconds}秒)";
        Log.Info(TAG, $"🎙️ 音声認識開始 (連続モード, モデル={modelType}, スレッド=4, デコード=beam_search) {verboseFlag}");

        // 前のバッファをクリア（前の認識データが混在するのを防ぐ）
        lock (_bufferLock)
        {
            _audioBuffer.Clear();
            Log.Debug(TAG, "マイク開始前：音声バッファをクリアしました");
        }

        _isListening = true; // ユーザーがマイクボタンをONにした
        _audioRecord?.StartRecording();
        RecognitionStarted?.Invoke(this, EventArgs.Empty);

        // 音声処理スレッドを開始
        _recordingThread = new Thread(ProcessAudioLoop)
        {
            IsBackground = true,
            Name = "SherpaRecordingThread"
        };
        _recordingThread.Start();
    }

    /// <summary>
    /// 音声認識を停止
    /// </summary>
    public void StopListening()
    {
        if (!_isListening)
            return;

        Log.Info(TAG, "🛑 音声認識停止 (ユーザーがマイクボタンをOFFにしました)");
        _isListening = false; // ユーザーがマイクボタンをOFFにした → 自動再開しない
        _audioRecord?.Stop();

        // スレッドの終了を待機（タイムアウト短縮して即座に停止）
        if (_recordingThread != null)
        {
            _recordingThread.Join(500); // 500ms で強制終了
            _recordingThread = null;
        }

        // バッファをクリア
        lock (_bufferLock)
        {
            _audioBuffer.Clear();
            Log.Debug(TAG, "音声バッファをクリアしました");
        }

        RecognitionStopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 音声処理ループ（バックグラウンドスレッド）
    /// </summary>
    private void ProcessAudioLoop()
    {
        try
        {
            if (_isStreamingModel)
            {
                ProcessStreamingAudioLoop();
            }
            else
            {
                ProcessOfflineAudioLoop();
            }
        }
        catch (Exception ex)
        {
            string errorMsg = $"音声処理エラー: {ex.Message}";
            Log.Error(TAG, errorMsg);
            Log.Error(TAG, $"StackTrace: {ex.StackTrace}");
            Error?.Invoke(this, errorMsg);
        }
    }

    /// <summary>
    /// ストリーミングモデル用の音声処理ループ
    /// </summary>
    private void ProcessStreamingAudioLoop()
    {
        int bufferSize = SampleRate * BufferSizeInMs / 1000;
        short[] audioBuffer = new short[bufferSize];
        float[] floatBuffer = new float[bufferSize];
        int frameCount = 0;

        Log.Info(TAG, $"🎙️ ストリーミング処理ループ開始 (バッファサイズ={bufferSize})");

        while (_isListening && _audioRecord != null && _onlineStream != null && _onlineRecognizer != null)
        {
            // マイクから音声データを読み込み
            int readCount = _audioRecord.Read(audioBuffer, 0, audioBuffer.Length);

            if (readCount > 0)
            {
                frameCount++;

                // short[] を float[] に変換
                for (int i = 0; i < readCount; i++)
                {
                    floatBuffer[i] = audioBuffer[i] / 32768.0f;
                }

                // ストリームに音声データを送信（リアルタイム）
                // AcceptWaveform(float[] samples, int sampleRate)
                _onlineStream.AcceptWaveform(floatBuffer, SampleRate);

                // デコードを実行（ストリーミングでは定期的にデコード必要）
                if (_onlineRecognizer.IsReady(_onlineStream))
                {
                    _onlineRecognizer.Decode(_onlineStream);
                }

                // 定期的に中間結果をログ出力（10フレームごと = 約100ms）
                if (frameCount % 10 == 0)
                {
                    var partialResult = _onlineRecognizer.GetResult(_onlineStream);
                    if (!string.IsNullOrEmpty(partialResult?.Text))
                    {
                        Log.Debug(TAG, $"📝 中間結果 (frame={frameCount}): 「{partialResult.Text}」");
                    }
                }

                // 発話区切りを検出
                if (_onlineRecognizer.IsEndpoint(_onlineStream))
                {
                    // 最終結果を取得
                    var result = _onlineRecognizer.GetResult(_onlineStream);
                    if (!string.IsNullOrEmpty(result?.Text))
                    {
                        Log.Info(TAG, $"✅ 【ストリーミング最終結果】「{result.Text}」");
                        FinalResult?.Invoke(this, result.Text);
                    }
                    else
                    {
                        Log.Debug(TAG, "⚠️ エンドポイント検出されましたが、テキストが空です");
                    }

                    // ストリームをリセット（次の発話用）
                    _onlineRecognizer.Reset(_onlineStream);
                    frameCount = 0;
                }
            }

            Thread.Sleep(10);
        }

        Log.Info(TAG, "🛑 ストリーミング処理ループ終了");
    }

    /// <summary>
    /// オフラインモデル用の音声処理ループ（既存のチャンク処理）
    /// </summary>
    private void ProcessOfflineAudioLoop()
    {
        int bufferSize = SampleRate * BufferSizeInMs / 1000;
        short[] audioBuffer = new short[bufferSize];
        float[] floatBuffer = new float[bufferSize];

        while (_isListening && _audioRecord != null)
        {
            // マイクから音声データを読み込み
            int readCount = _audioRecord.Read(audioBuffer, 0, audioBuffer.Length);

            if (readCount > 0)
            {
                // short[] を float[] に変換（Sherpa-ONNXはfloat配列を期待）
                for (int i = 0; i < readCount; i++)
                {
                    floatBuffer[i] = audioBuffer[i] / 32768.0f; // 正規化 [-1.0, 1.0]
                }

                // 音声バッファに追加
                lock (_bufferLock)
                {
                    for (int i = 0; i < readCount; i++)
                    {
                        _audioBuffer.Add(floatBuffer[i]);
                    }

                    // 一定時間（ChunkDurationSeconds）分のデータが溜まったら認識実行
                    int chunkSize = SampleRate * ChunkDurationSeconds;
                    if (_audioBuffer.Count >= chunkSize)
                    {
                        // チャンクを取得（全体をコピー）
                        var chunk = _audioBuffer.Take(chunkSize).ToArray();

                        // 音声レベルの計算（デバッグ用）
                        float maxAmp = chunk.Max(Math.Abs);
                        float avgAmp = chunk.Average(Math.Abs);

                        // スライディングウィンドウ: オーバーラップ分を残して古いデータを削除
                        int removeCount = (int)(chunkSize * (1.0f - ChunkOverlapRatio));
                        _audioBuffer.RemoveRange(0, removeCount);

                        if (VerboseLoggingEnabled)
                        {
                            Log.Info(TAG, $"📊 チャンク処理: {chunk.Length}サンプル | レベル max={maxAmp:F4}, avg={avgAmp:F4} | バッファ残={_audioBuffer.Count}");
                        }

                        // 非同期で認識実行（UIスレッドをブロックしない）
                        Task.Run(() =>
                        {
                            ProcessAudioChunk(chunk);

                            // ユーザーがマイクボタンをONのままなら、自動で次の認識に進む
                            if (_isListening)
                            {
                                Log.Info(TAG, "🔄 連続認識: 次の認識に進みます");
                            }
                        });
                    }
                }
            }

            // 短い待機（CPUを占有しないため）
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// 音声チャンクを認識
    /// </summary>
    private void ProcessAudioChunk(float[] audioChunk)
    {
        var startTime = DateTime.Now;
        try
        {
            if (_recognizer == null)
            {
                Log.Warn(TAG, "❌ Recognizerがnullです");
                return;
            }

            // ノイズ判定: 平均振幅で判定（スパイクノイズを除外）
            float maxAmplitude = audioChunk.Max(Math.Abs);
            float avgAmplitude = audioChunk.Average(Math.Abs);

            const float MinAvgAmplitude = 0.01f; // 最小平均振幅（厳格な閾値）

            // 判定ロジック: 平均振幅が低い場合はスキップ（スパイクノイズを除外）
            if (avgAmplitude < MinAvgAmplitude)
            {
                if (VerboseLoggingEnabled)
                {
                    Log.Debug(TAG, $"🔇 音声レベル低 - スキップ (avg={avgAmplitude:F4} < {MinAvgAmplitude}) | ピーク={maxAmplitude:F4}");
                }
                return;
            }

            // スパイクノイズの検出: ピークが高いが平均が低い場合
            if (maxAmplitude > 0.3f && avgAmplitude < 0.02f)
            {
                if (VerboseLoggingEnabled)
                {
                    Log.Debug(TAG, $"🔊 スパイクノイズ判定 - スキップ (max={maxAmplitude:F4}, avg={avgAmplitude:F4})");
                }
                return;
            }

            if (VerboseLoggingEnabled)
            {
                Log.Debug(TAG, $"🎤 音声検出 - 認識開始 (サンプル数={audioChunk.Length}, max={maxAmplitude:F4}, avg={avgAmplitude:F4})");
            }

            // OfflineStreamを作成
            var stream = _recognizer.CreateStream();

            // 音声データを送信（パラメータ順序: samples, sampleRate）
            stream.AcceptWaveform(audioChunk, SampleRate);

            // 認識実行
            var decodeStart = DateTime.Now;
            _recognizer.Decode(stream);
            var decodeTime = (DateTime.Now - decodeStart).TotalMilliseconds;

            // 結果を取得
            var recognitionResult = _recognizer.GetResult(stream);

            var totalTime = (DateTime.Now - startTime).TotalMilliseconds;

            if (!string.IsNullOrEmpty(recognitionResult?.Text))
            {
                Log.Info(TAG, $"✅ 【完全結果】「{recognitionResult.Text}」");
                if (VerboseLoggingEnabled)
                {
                    Log.Debug(TAG, $"   詳細: 認識={decodeTime:F0}ms, 合計={totalTime:F0}ms, サンプル数={audioChunk.Length}");
                }
                // 完全結果を通知
                FinalResult?.Invoke(this, recognitionResult.Text);
            }
            else
            {
                if (VerboseLoggingEnabled)
                {
                    Log.Debug(TAG, $"⚠️ 認識結果なし (無音または認識失敗) (認識={decodeTime:F0}ms, 合計={totalTime:F0}ms)");
                }
            }

            // ストリームを解放
            stream.Dispose();
        }
        catch (Exception ex)
        {
            var totalTime = (DateTime.Now - startTime).TotalMilliseconds;
            string errorMsg = $"❌ 認識エラー: {ex.Message} ({totalTime:F0}ms)";
            Log.Error(TAG, errorMsg);
            Log.Error(TAG, $"StackTrace: {ex.StackTrace}");
            Error?.Invoke(this, errorMsg);
        }
    }

    /// <summary>
    /// モデルファイルの存在を確認
    /// </summary>
    private bool CheckModelFiles(string modelPath, bool isFilePath)
    {
        try
        {
            Log.Info(TAG, $"モデルファイル確認: {modelPath}");

            if (isFilePath)
            {
                // ファイルシステムパスのチェック
                if (!Directory.Exists(modelPath))
                {
                    Log.Error(TAG, $"モデルディレクトリが存在しません: {modelPath}");
                    return false;
                }

                string[] requiredFiles = {
                    "encoder-epoch-99-avg-1.int8.onnx",
                    "decoder-epoch-99-avg-1.int8.onnx",
                    "joiner-epoch-99-avg-1.int8.onnx",
                    "tokens.txt"
                };

                foreach (string file in requiredFiles)
                {
                    string filePath = System.IO.Path.Combine(modelPath, file);
                    if (!System.IO.File.Exists(filePath))
                    {
                        Log.Error(TAG, $"必須ファイルが見つかりません: {filePath}");
                        return false;
                    }
                    Log.Debug(TAG, $"  ✓ {file}");
                }

                Log.Info(TAG, $"すべてのファイルが存在します (FilePath)");
                return true;
            }
            else
            {
                // assetsからモデルファイルをチェック
                var assetFiles = _context.Assets?.List(modelPath);

                if (assetFiles != null && assetFiles.Length > 0)
                {
                    Log.Info(TAG, $"見つかったファイル数: {assetFiles.Length}");
                    foreach (var file in assetFiles)
                    {
                        Log.Debug(TAG, $"  - {file}");
                    }
                    return true;
                }
                else
                {
                    Log.Error(TAG, $"モデルファイルが見つかりません: {modelPath}");

                    // デバッグ用: assetsのルートディレクトリを確認
                    var rootFiles = _context.Assets?.List("");
                    if (rootFiles != null)
                    {
                        Log.Info(TAG, "Assets ルートディレクトリ:");
                        foreach (var file in rootFiles)
                        {
                            Log.Debug(TAG, $"  - {file}");
                        }
                    }

                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"モデルファイル確認エラー: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 認識器とオーディオバッファをクリア（モデル切り替え用）
    /// </summary>
    private void DisposeRecognizer()
    {
        try
        {
            // オンラインストリームを破棄
            if (_onlineStream != null)
            {
                _onlineStream.Dispose();
                _onlineStream = null;
                Log.Info(TAG, "OnlineStreamを破棄しました");
            }

            // オンライン認識器を破棄
            if (_onlineRecognizer != null)
            {
                _onlineRecognizer.Dispose();
                _onlineRecognizer = null;
                Log.Info(TAG, "OnlineRecognizerを破棄しました");
            }

            // オフライン認識器を破棄
            if (_recognizer != null)
            {
                _recognizer.Dispose();
                _recognizer = null;
                Log.Info(TAG, "OfflineRecognizerを破棄しました");
            }

            // オーディオバッファをクリア
            lock (_bufferLock)
            {
                _audioBuffer.Clear();
            }

            _isInitialized = false;
            _isStreamingModel = false;
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"認識器破棄エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// リソースを解放（IDisposable実装）
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// リソース解放の実装
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopListening();

            // Sherpa-ONNXリソースを解放
            DisposeRecognizer();

            if (_audioRecord != null)
            {
                if (_audioRecord.State == State.Initialized)
                {
                    _audioRecord.Stop();
                }
                _audioRecord.Release();
                _audioRecord.Dispose();
                _audioRecord = null;
            }

            _isInitialized = false;
        }
    }

    /// <summary>
    /// ファイナライザー（念のため）
    /// </summary>
    ~SherpaRealtimeService()
    {
        Dispose(false);
    }
}
