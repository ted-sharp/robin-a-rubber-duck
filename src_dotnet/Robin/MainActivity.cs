using Android.Content.PM;
using Android.Views;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.DrawerLayout.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using Robin.Adapters;
using Robin.Models;
using Robin.Services;

namespace Robin;

[Activity(Label = "@string/app_name", MainLauncher = true, Theme = "@style/AppTheme")]
public class MainActivity : AppCompatActivity
{
    private const int RequestRecordAudioPermission = 1;

    // ファイルピッカー用
    private ActivityResultLauncher? _configFilePickerLauncher;
    private string? _currentConfigFileType; // "llm", "azure", "fasterwhisper" など
    private Action<string>? _llmConfigCallback; // LLM設定ファイル選択時のコールバック
    private Action<string>? _azureConfigCallback; // Azure STT設定ファイル選択時のコールバック
    private Action<string>? _fasterWhisperConfigCallback; // Faster Whisper設定ファイル選択時のコールバック

    private DrawerLayout? _drawerLayout;
    private NavigationView? _navigationView;
    private RecyclerView? _recyclerView;
    private FloatingActionButton? _micButton;
    private FloatingActionButton? _sendButton;
    private TextView? _statusText;
    private TextView? _welcomeMessageText;
    private View? _swipeHint;
    private LinearLayout? _llmModelContainer;
    private ImageButton? _modelToggleButton;
    private TextView? _currentModelText;
    private LinearLayout? _asrModelContainer;
    private ImageButton? _asrToggleButton;
    private TextView? _currentAsrModelText;
    private LinearLayout? _llmErrorContainer;
    private ImageView? _llmErrorIcon;
    private TextView? _llmErrorMessage;

    private MessageAdapter? _messageAdapter;
    private ConversationService? _conversationService;
    private VoiceInputService? _voiceInputService;
    private SherpaRealtimeService? _sherpaService;
    private OpenAIService? _openAIService;
    private SettingsService? _settingsService;
    private RecognizedInputBuffer? _inputBuffer;
    private SemanticValidationService? _semanticValidationService;
    private CancellationTokenSource? _semanticValidationCancellation; // 意味解析キャンセル用

    // 音声認識エンジンの選択
    private readonly bool _useSherpaOnnx = true; // true: Sherpa-ONNX, false: Android標準
    private string? _currentModelName = null; // 現在のモデル名
    private string _selectedLanguage = "ja"; // 選択言語（デフォルト: 日本語）
    private string? _azureSttConfigPath = null; // Azure STT設定ファイルパス
    private AzureSttService? _azureSttService = null; // Azure STTサービス
    private FasterWhisperService? _fasterWhisperService = null; // Faster Whisperサービス

    // LLMエラー状態管理
#pragma warning disable CS0414 // フィールドが割り当てられていますが値が使用されていません (将来使用予定)
    private bool _llmHasError = false; // LLM接続エラーが発生しているか
#pragma warning restore CS0414
    private bool _llmOnlyTranscriptionMode = false; // 音声認識のみモード（LLM無視）

    // 利用可能なモデル (model-prep-config.jsonの並び順に合わせる)
    private readonly string[] _availableModels = new[]
    {
        "android-default", // Androidデフォルト音声認識
        "azure-stt", // Azure Speech-to-Text API
        "faster-whisper", // Faster Whisper (LAN内サーバー)
        "sherpa-onnx-streaming-zipformer-ar_en_id_ja_ru_th_vi_zh-2025-02-10",
        "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
        "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8",
        "sherpa-onnx-whisper-tiny",
        "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09"
    };

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        // システムプロンプトをテキストファイルから読み込み
        SystemPrompts.LoadFromFiles(this);

        InitializeViews();
        InitializeServices();
        SetupConfigFilePickerLauncher();
        SetupRecyclerView();
        SetupDrawerNavigation();
        SetupMicButton();  // ← このタイミングで登録（Sherpaの初期化と並行実行）
        SetupSendButton();
        SetupBackPressHandler();
        CheckPermissions();

        // 起動時にドロワーを開く（スワイプヒントは非表示に）
        _drawerLayout?.OpenDrawer((int)GravityFlags.Start);
        if (_swipeHint != null)
        {
            _swipeHint.Alpha = 0f; // ドロワーが開いているのでヒントは非表示
        }

        // 初回起動時にウェルカムメッセージを表示
        ShowWelcomeMessageIfEmpty();
    }

    private void ShowWelcomeMessageIfEmpty()
    {
        // 会話ログが空の場合、ウェルカムメッセージをTextViewに表示
        if (_conversationService == null || _conversationService.GetMessages().Count != 0)
            return;

        if (_welcomeMessageText == null)
            return;

        // 複数のメッセージを順番に追加（見た目を改善）
        var messages = new[]
        {
            GetString(Resource.String.welcome_intro),
            "",
            GetString(Resource.String.welcome_concept_title),
            GetString(Resource.String.welcome_concept),
            "",
            GetString(Resource.String.welcome_usage_title),
            GetString(Resource.String.welcome_usage_step1),
            GetString(Resource.String.welcome_usage_step2),
            GetString(Resource.String.welcome_usage_step3),
            "",
            GetString(Resource.String.welcome_benefits_title),
            GetString(Resource.String.welcome_benefits),
            "",
            GetString(Resource.String.welcome_start)
        };

        // ウェルカムメッセージをTextViewに表示
        var fullWelcomeMessage = string.Join("\n", messages);
        RunOnUiThread(() =>
        {
            _welcomeMessageText.Text = fullWelcomeMessage;
            _welcomeMessageText.Visibility = ViewStates.Visible;
        });
    }

    private void InitializeViews()
    {
        _drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
        _navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
        _recyclerView = FindViewById<RecyclerView>(Resource.Id.chat_recycler_view);
        _micButton = FindViewById<FloatingActionButton>(Resource.Id.mic_button);
        _sendButton = FindViewById<FloatingActionButton>(Resource.Id.send_button);
        _statusText = FindViewById<TextView>(Resource.Id.status_text);
        _welcomeMessageText = FindViewById<TextView>(Resource.Id.welcome_message_text);
        _swipeHint = FindViewById(Resource.Id.swipe_hint);
        _llmModelContainer = FindViewById<LinearLayout>(Resource.Id.llm_model_container);
        _modelToggleButton = FindViewById<ImageButton>(Resource.Id.model_toggle_button);
        _currentModelText = FindViewById<TextView>(Resource.Id.current_model_text);
        _asrModelContainer = FindViewById<LinearLayout>(Resource.Id.asr_model_container);
        _asrToggleButton = FindViewById<ImageButton>(Resource.Id.asr_toggle_button);
        _currentAsrModelText = FindViewById<TextView>(Resource.Id.current_asr_model_text);
        _llmErrorContainer = FindViewById<LinearLayout>(Resource.Id.llm_error_container);
        _llmErrorIcon = FindViewById<ImageView>(Resource.Id.llm_error_icon);
        _llmErrorMessage = FindViewById<TextView>(Resource.Id.llm_error_message);

        // LLMモデルコンテナ全体のタップイベント設定
        if (_llmModelContainer != null)
        {
            _llmModelContainer.Click += OnModelToggleButtonClick;
        }

        // ASRモデルコンテナ全体のタップイベント設定
        if (_asrModelContainer != null)
        {
            _asrModelContainer.Click += OnAsrToggleButtonClick;
        }

        // LLMエラーコンテナのタップイベント設定
        if (_llmErrorContainer != null)
        {
            _llmErrorContainer.Click += OnLlmErrorIconClick;
        }

        // スワイプヒント（ドロワーハンドル）のタップイベント設定
        if (_swipeHint != null)
        {
            _swipeHint.Click += OnSwipeHintClick;
        }
    }

    private void InitializeServices()
    {
        // バッファの初期化
        _inputBuffer = new RecognizedInputBuffer(timeoutMs: 2000); // 2秒のタイムアウト
        _inputBuffer.BufferReady += OnInputBufferReady;

        _conversationService = new ConversationService(this);
        _conversationService.MessageAdded += OnMessageAdded;

        _voiceInputService = new VoiceInputService(this);
        _voiceInputService.RecognitionStarted += OnRecognitionStarted;
        _voiceInputService.RecognitionStopped += OnRecognitionStopped;
        _voiceInputService.RecognitionResult += OnRecognitionResult;
        _voiceInputService.RecognitionError += OnRecognitionError;

        // Azure STTサービスの初期化（イベントハンドラーのみ設定、実際の初期化は設定ファイル選択時）
        _azureSttService = new AzureSttService(this);
        _azureSttService.RecognitionStarted += OnRecognitionStarted;
        _azureSttService.RecognitionStopped += OnRecognitionStopped;
        _azureSttService.RecognitionResult += OnRecognitionResult;
        _azureSttService.RecognitionError += OnRecognitionError;

        // Faster Whisperサービスの初期化
        _fasterWhisperService = new FasterWhisperService(this);
        _fasterWhisperService.RecognitionStarted += OnRecognitionStarted;
        _fasterWhisperService.RecognitionStopped += OnRecognitionStopped;
        _fasterWhisperService.RecognitionResult += OnRecognitionResult;
        _fasterWhisperService.RecognitionError += OnRecognitionError;

        // Sherpa-ONNXサービスの初期化
        InitializeSherpaService();

        // 設定サービス初期化
        _settingsService = new SettingsService(this);

        // LLMプロバイダー初期化
        InitializeLLMService();

        // セマンティック検証サービスの初期化（SettingsService を注入してカスタムプロンプット対応）
        _semanticValidationService = new SemanticValidationService(_openAIService, _settingsService);

        // システムプロンプト設定を読み込んで適用
        ApplySystemPromptSettings();
    }

    /// <summary>
    /// 設定ファイルピッカーランチャーの初期化
    /// </summary>
    private void SetupConfigFilePickerLauncher()
    {
        _configFilePickerLauncher = RegisterForActivityResult(
            new ActivityResultContracts.GetContent(),
            new ConfigFilePickerCallback(this)
        );
    }

    /// <summary>
    /// Sherpa-ONNXサービスの初期化
    /// </summary>
    private void InitializeSherpaService()
    {
        // Sherpa-ONNXサービス
        _sherpaService = new SherpaRealtimeService(this);
        _sherpaService.RecognitionStarted += OnSherpaRecognitionStarted;
        _sherpaService.RecognitionStopped += OnSherpaRecognitionStopped;
        _sherpaService.FinalResult += OnSherpaFinalResult;
        _sherpaService.Error += OnSherpaError;
        _sherpaService.InitializationProgress += OnSherpaInitializationProgress;

        // 非同期初期化（設定から選択されているモデルを読み込む）
        Task.Run(async () => await InitializeSherpaAsync());
    }

    /// <summary>
    /// Sherpa-ONNXサービスの非同期初期化処理
    /// </summary>
    private async Task InitializeSherpaAsync()
    {
        try
        {
            // UI更新は最小限に（初期化ステータスは最初だけ表示）
            RunOnUiThread(() =>
            {
                if (_statusText != null)
                {
                    _statusText.Text = "Sherpa-ONNX初期化中...";
                    _statusText.Visibility = ViewStates.Visible;
                }
            });

            // 初期化を非同期で実行（UI をブロックしない）
            var (initialized, modelName) = await Task.Run(async () =>
                await TryInitializeSherpaModelsAsync()
            );

            if (initialized && modelName != null)
            {
                OnSherpaInitializeSuccess(modelName);
            }
            else
            {
                OnSherpaInitializeFailure();
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"Sherpa初期化エラー: {ex.Message}");
            OnSherpaInitializeFailure();
        }
    }

    /// <summary>
    /// 利用可能なSherpaモデルを順に初期化を試行
    /// </summary>
    private async Task<(bool, string?)> TryInitializeSherpaModelsAsync()
    {
        // 設定から選択されているモデルを読み込む
        var sttSettings = _settingsService?.LoadSTTProviderSettings();
        var selectedModel = sttSettings?.ModelName;

        // 初期化を試みるモデルのリスト（設定から選択されているモデルを最優先）
        var modelsToTry = new List<string>();

        // 選択されているモデルを最初に追加
        if (!string.IsNullOrWhiteSpace(selectedModel))
        {
            modelsToTry.Add(selectedModel);
            Android.Util.Log.Info("MainActivity", $"設定から選択されているモデル: {selectedModel}");
        }

        // フォールバック用の他のモデルを追加
        modelsToTry.AddRange(new[]
        {
            "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
            "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
            "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8",
            "sherpa-onnx-whisper-tiny"
        });

        // 重複排除（選択されているモデルが複数回含まれないようにする）
        var uniqueModels = new Dictionary<string, bool>();
        foreach (var model in modelsToTry)
        {
            if (!uniqueModels.ContainsKey(model))
            {
                uniqueModels.Add(model, true);
            }
        }

        foreach (var modelName in uniqueModels.Keys)
        {
            Android.Util.Log.Info("MainActivity", $"Sherpa初期化試行: {modelName}");
            RunOnUiThread(() =>
            {
                if (_statusText != null)
                {
                    _statusText.Text = $"初期化中: {modelName}";
                }
            });

            try
            {
                bool initialized = await _sherpaService!.InitializeAsync(modelName, isFilePath: false);
                if (initialized)
                {
                    Android.Util.Log.Info("MainActivity", $"Sherpa初期化成功: {modelName}");
                    return (true, modelName);
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("MainActivity", $"Sherpa初期化失敗 {modelName}: {ex.Message}");
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Sherpa初期化成功時の処理
    /// </summary>
    private void OnSherpaInitializeSuccess(string modelName)
    {
        RunOnUiThread(() =>
        {
            if (_statusText != null)
            {
                _statusText.Visibility = ViewStates.Gone;
            }
            _currentModelName = modelName;
            UpdateAsrModelDisplay();
            ShowToast($"Sherpa-ONNX初期化完了: {modelName}");
        });
    }

    /// <summary>
    /// Sherpa初期化失敗時の処理（フォールバック）
    /// </summary>
    private void OnSherpaInitializeFailure()
    {
        Android.Util.Log.Error("MainActivity", "すべてのモデルで初期化失敗 - Android標準を使用");
        RunOnUiThread(() =>
        {
            if (_statusText != null)
            {
                _statusText.Visibility = ViewStates.Gone;
            }
            _currentModelName = "Android Standard (Offline)";
            UpdateAsrModelDisplay();
            ShowToast("モデル初期化失敗。Android標準を使用します。");
        });
    }

    /// <summary>
    /// LLMプロバイダーサービスの初期化
    /// </summary>
    private void InitializeLLMService()
    {
        var llmSettings = _settingsService!.LoadLLMProviderSettings();

        if (llmSettings.IsEnabled && !string.IsNullOrWhiteSpace(llmSettings.ModelName))
        {
            InitializeLLMProvider(llmSettings);
        }
        else
        {
            // モック版: APIキーなしで動作
            _openAIService = new OpenAIService("mock-api-key");
        }

        // LLMモデル表示を更新
        UpdateCurrentModelDisplay();
    }

    /// <summary>
    /// 指定されたLLMプロバイダーを初期化
    /// </summary>
    private void InitializeLLMProvider(LLMProviderSettings llmSettings)
    {
        try
        {
            if (llmSettings.Provider == "openai")
            {
                if (string.IsNullOrWhiteSpace(llmSettings.ApiKey))
                {
                    throw new InvalidOperationException("OpenAI APIキーが設定されていません");
                }
                _openAIService = new OpenAIService(llmSettings.ApiKey, llmSettings.ModelName);
                Android.Util.Log.Info("MainActivity", $"OpenAI初期化: {llmSettings.ModelName}");
                ShowToast($"OpenAI接続: {llmSettings.ModelName}");
            }
            else if (llmSettings.Provider.StartsWith("lm-studio"))
            {
                _openAIService = new OpenAIService(llmSettings.Endpoint, llmSettings.ModelName, isLMStudio: true);
                Android.Util.Log.Info("MainActivity", $"LM Studio初期化 [{llmSettings.Provider}]: {llmSettings.Endpoint}, Model: {llmSettings.ModelName}");
                ShowToast($"LM Studio接続: {llmSettings.ModelName}");
            }
            else if (llmSettings.Provider == "azure-openai")
            {
                // Azure OpenAI は汎用コンストラクタを使用
                _openAIService = new OpenAIService(llmSettings.Provider, llmSettings.Endpoint, llmSettings.ModelName, llmSettings.ApiKey);
                Android.Util.Log.Info("MainActivity", $"Azure OpenAI初期化: {llmSettings.Endpoint}, Deployment: {llmSettings.ModelName}");
                ShowToast($"Azure OpenAI接続: {llmSettings.ModelName}");
            }
            else if (llmSettings.Provider == "claude")
            {
                // Claude は汎用コンストラクタを使用（将来の実装のためのプレースホルダー）
                _openAIService = new OpenAIService(llmSettings.Provider, llmSettings.Endpoint, llmSettings.ModelName, llmSettings.ApiKey);
                Android.Util.Log.Info("MainActivity", $"Claude初期化: {llmSettings.ModelName}");
                ShowToast($"Claude接続: {llmSettings.ModelName}");
            }
            else
            {
                throw new InvalidOperationException($"未サポートのプロバイダー: {llmSettings.Provider}");
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"LLM初期化失敗 [Provider: {llmSettings.Provider}, Endpoint: {llmSettings.Endpoint}]: {ex.Message}");
            Android.Util.Log.Error("MainActivity", $"スタックトレース: {ex.StackTrace}");
            // フォールバック: モック版
            _openAIService = new OpenAIService("mock-api-key");
            ShowToast($"LLM接続失敗: {ex.Message}。モック版を使用します。");
        }
    }

    /// <summary>
    /// 保存されているシステムプロンプト設定を読み込んで OpenAIService に適用
    /// </summary>
    private void ApplySystemPromptSettings()
    {
        if (_settingsService == null || _openAIService == null)
            return;

        try
        {
            var promptSettings = _settingsService.LoadSystemPromptSettings();
            var useUserContext = _settingsService.LoadUseUserContext();
            var userContext = _settingsService.LoadUserContext();

            string finalPrompt;

            if (promptSettings.UseCustomPrompts)
            {
                // カスタムプロンプトを使用
                finalPrompt = promptSettings.ConversationPrompt ?? SystemPrompts.ConversationSystemPrompt;
                Android.Util.Log.Info("MainActivity", "カスタム Conversation プロンプトを適用");
            }
            else
            {
                // デフォルトプロンプトを使用
                finalPrompt = SystemPrompts.ConversationSystemPrompt;
                Android.Util.Log.Info("MainActivity", "デフォルト Conversation プロンプトを適用");
            }

            // ユーザーコンテキストを追加
            if (useUserContext && !string.IsNullOrWhiteSpace(userContext))
            {
                finalPrompt += $"\n\n## ユーザーについて\n以下のユーザー情報を考慮して、より適切な支援を提供してください：\n{userContext}";
                Android.Util.Log.Info("MainActivity", $"ユーザーコンテキストを追加: {userContext.Length} 文字");
            }

            _openAIService.SetSystemPrompt(finalPrompt);

            // SemanticValidationService もカスタムプロンプトを認識するように設定
            // （SemanticValidationService 内部で自動的に処理される）
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("MainActivity", $"システムプロンプト設定の適用に失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// モデルファイルが存在するかチェック
    /// </summary>
    private bool IsModelAvailable(string modelName)
    {
        // Androidデフォルト、クラウドサービス、Faster Whisperは常に利用可能
        if (modelName == "android-default" || modelName == "azure-stt" || modelName == "faster-whisper")
        {
            return true;
        }

        // Sherpaモデルの場合、assetsフォルダ内の必須ファイルを確認
        try
        {
            using var assetManager = Assets;
            var files = assetManager?.List(modelName);

            // フォルダが存在し、ファイルが含まれているか確認
            if (files != null && files.Length > 0)
            {
                Android.Util.Log.Debug("MainActivity", $"モデル {modelName} が利用可能: {files.Length}ファイル");
                return true;
            }
            else
            {
                Android.Util.Log.Debug("MainActivity", $"モデル {modelName} は存在しません");
                return false;
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("MainActivity", $"モデル存在確認エラー {modelName}: {ex.Message}");
            return false;
        }
    }

    private void ShowModelSelectionDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("音声認識モデルを選択");

        // モデルの基本表示名とサイズ (model-prep-config.jsonの並び順に合わせる)
        var baseDisplayNames = new string[]
        {
            "Android デフォルト音声認識 - オンライン",
            "Azure STT (クラウドAPI) - オンライン専用",
            "Faster Whisper (LAN内サーバー) - 要サーバー設定",
            "Streaming Zipformer (8言語) - 247MB",
            "Zipformer Ja (高精度日本語) - 680MB",
            "Nemo CTC (日本語) - 625MB",
            "Whisper Tiny (多言語) - 104MB",
            "SenseVoice (多言語) - 238MB"
        };

        // モデルの存在確認と表示名の更新
        var modelDisplayNames = new string[baseDisplayNames.Length];
        var modelAvailability = new bool[_availableModels.Length];

        for (int i = 0; i < _availableModels.Length; i++)
        {
            bool isAvailable = IsModelAvailable(_availableModels[i]);
            modelAvailability[i] = isAvailable;

            if (isAvailable)
            {
                modelDisplayNames[i] = baseDisplayNames[i];
            }
            else
            {
                modelDisplayNames[i] = baseDisplayNames[i] + " (未ダウンロード)";
            }
        }

        // 現在のモデルのインデックスを取得
        int checkedItem = -1;
        if (!string.IsNullOrEmpty(_currentModelName))
        {
            for (int i = 0; i < _availableModels.Length; i++)
            {
                if (_availableModels[i] == _currentModelName)
                {
                    checkedItem = i;
                    break;
                }
            }
        }

        AndroidX.AppCompat.App.AlertDialog? dialog = null;
        builder.SetSingleChoiceItems(modelDisplayNames, checkedItem, (sender, e) =>
        {
            // モデルが利用可能かチェック
            if (!modelAvailability[e.Which])
            {
                ShowToast("このモデルはダウンロードされていません");
                return;
            }

            var selectedModel = _availableModels[e.Which];
            dialog?.Dismiss();

            // Androidデフォルト音声認識の場合
            if (selectedModel == "android-default")
            {
                LoadAndroidDefaultStt();
            }
            // Azure STTの場合は設定ダイアログを表示
            else if (selectedModel == "azure-stt")
            {
                ShowAzureSttConfigDialog();
            }
            // Faster Whisperの場合はサーバーURL入力ダイアログを表示
            else if (selectedModel == "faster-whisper")
            {
                ShowFasterWhisperServerDialog();
            }
            // SenseVoiceまたはWhisperの場合は言語選択ダイアログを表示
            else if (selectedModel.Contains("sense-voice") || selectedModel.Contains("whisper"))
            {
                ShowLanguageSelectionDialog(selectedModel);
            }
            else
            {
                LoadModel(selectedModel);
            }
        });

        builder.SetNegativeButton("キャンセル", (sender, e) =>
        {
            dialog?.Dismiss();
        });

        dialog = builder.Create();
        dialog?.Show();
    }

    private void ShowLlmModelSelectionDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("LLMモデルを選択");

        // LLMプロバイダーの選択肢
        var llmProviderNames = new string[]
        {
            "OpenAI",
            "Azure OpenAI",
            "LM Studio 1",
            "LM Studio 2",
            "Claude (Anthropic)"
        };

        // プロバイダー識別子の配列
        var llmProviderIds = new string[]
        {
            "openai",
            "azure-openai",
            "lm-studio-1",
            "lm-studio-2",
            "claude"
        };

        // 現在選択されているプロバイダーを取得
        var currentSettings = _settingsService?.LoadLLMProviderSettings();
        var currentProvider = currentSettings?.Provider ?? "";

        // 現在選択されているインデックスを見つける
        int selectedIndex = -1;
        for (int i = 0; i < llmProviderIds.Length; i++)
        {
            if (llmProviderIds[i] == currentProvider)
            {
                selectedIndex = i;
                break;
            }
        }

        AndroidX.AppCompat.App.AlertDialog? dialog = null;
        builder.SetSingleChoiceItems(llmProviderNames, selectedIndex, (sender, e) =>
        {
            dialog?.Dismiss();

            switch (e.Which)
            {
                case 0: // OpenAI
                    ShowOpenAIConfigDialog();
                    break;
                case 1: // Azure OpenAI
                    ShowAzureOpenAIConfigDialog();
                    break;
                case 2: // LM Studio 1
                    ShowLMStudioConfigDialog("lm-studio-1");
                    break;
                case 3: // LM Studio 2
                    ShowLMStudioConfigDialog("lm-studio-2");
                    break;
                case 4: // Claude
                    ShowClaudeConfigDialog();
                    break;
            }
        });

        builder.SetNegativeButton("キャンセル", (sender, e) =>
        {
            dialog?.Dismiss();
        });

        dialog = builder.Create();
        dialog?.Show();
    }

    private void LoadLlmProvider(string providerKey)
    {
        RunOnUiThread(() =>
        {
            try
            {
                if (_settingsService == null)
                {
                    Toast.MakeText(this, "設定サービスが初期化されていません", ToastLength.Short)?.Show();
                    return;
                }

                // コレクションを読み込み
                var collection = _settingsService.LoadLLMProviderCollection();

                // 現在のプロバイダーを更新
                collection.CurrentProvider = providerKey;

                // 保存
                _settingsService.SaveLLMProviderCollection(collection);

                // 現在の設定を取得して表示
                var currentSettings = collection.GetCurrentSettings();
                if (currentSettings != null)
                {
                    var displayName = providerKey == "lmStudio1" ? "LM Studio 1（自宅）" : "LM Studio 2（職場）";

                    // OpenAIServiceを再初期化
                    InitializeLLMProvider(currentSettings);

                    _statusText?.SetText($"{displayName}に切り替え: {currentSettings.Endpoint}", TextView.BufferType.Normal);
                    Android.Util.Log.Info("MainActivity", $"LLMプロバイダーを{providerKey}に切り替え: {currentSettings.Endpoint}");

                    Toast.MakeText(this, $"{displayName}に切り替えました", ToastLength.Short)?.Show();
                }
            }
            catch (Exception ex)
            {
                _statusText?.SetText($"LLM切り替えエラー: {ex.Message}", TextView.BufferType.Normal);
                Android.Util.Log.Error("MainActivity", $"LLM切り替えエラー: {ex.Message}");
                Toast.MakeText(this, $"エラー: {ex.Message}", ToastLength.Short)?.Show();
            }
        });
    }

    private void ShowLlmConfigFileSelectionDialog()
    {
        _currentConfigFileType = "llm";
        _configFilePickerLauncher?.Launch("application/json");
    }

    private void LoadLlmConfigFromFile(string configFilePath)
    {
        Task.Run(() =>
        {
            try
            {
                // JSONファイルを読み込み
                var jsonContent = File.ReadAllText(configFilePath);
                var loadedSettings = System.Text.Json.JsonSerializer.Deserialize<LLMProviderSettings>(jsonContent);

                if (loadedSettings == null)
                {
                    RunOnUiThread(() =>
                    {
                        Toast.MakeText(this, "設定ファイルの読み込みに失敗しました", ToastLength.Short)?.Show();
                    });
                    return;
                }

                RunOnUiThread(() =>
                {
                    if (_settingsService == null)
                    {
                        Toast.MakeText(this, "設定サービスが初期化されていません", ToastLength.Short)?.Show();
                        return;
                    }

                    // コレクションを読み込み
                    var collection = _settingsService.LoadLLMProviderCollection();

                    // ファイルから読み込んだ設定をlmStudio1に保存（上書き）
                    collection.LmStudio1 = loadedSettings;
                    collection.CurrentProvider = "lmStudio1";

                    // 保存
                    _settingsService.SaveLLMProviderCollection(collection);

                    _statusText?.SetText($"LLM設定読込完了: {Path.GetFileName(configFilePath)}", TextView.BufferType.Normal);
                    Android.Util.Log.Info("MainActivity", $"LLM設定読込: {configFilePath} -> lmStudio1");

                    Toast.MakeText(this, $"設定ファイルを読み込みました（LM Studio 1に保存）", ToastLength.Short)?.Show();
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    _statusText?.SetText($"LLM設定読込エラー: {ex.Message}", TextView.BufferType.Normal);
                    Android.Util.Log.Error("MainActivity", $"LLM設定読込エラー: {ex.Message}");
                    Toast.MakeText(this, $"エラー: {ex.Message}", ToastLength.Short)?.Show();
                });
            }
        });
    }

    private void ShowOpenAIConfigDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("OpenAI設定");

        var layout = new Android.Widget.LinearLayout(this)
        {
            Orientation = Android.Widget.Orientation.Vertical
        };
        layout.SetPadding(50, 40, 50, 10);

        // API Key入力
        var apiKeyLabel = new Android.Widget.TextView(this) { Text = "API Key:" };
        layout.AddView(apiKeyLabel);
        var apiKeyInput = new Android.Widget.EditText(this)
        {
            Hint = "sk-...",
            InputType = Android.Text.InputTypes.TextVariationPassword
        };
        layout.AddView(apiKeyInput);

        // Model入力
        var modelLabel = new Android.Widget.TextView(this) { Text = "Model:" };
        layout.AddView(modelLabel);
        var modelInput = new Android.Widget.EditText(this)
        {
            Hint = "gpt-4o, gpt-4o-mini, gpt-3.5-turbo など",
            Text = "gpt-4o-mini"
        };
        layout.AddView(modelInput);

        // 設定ファイル読み込みボタン
        var loadButton = new Android.Widget.Button(this) { Text = "設定ファイルから読み込み" };
        loadButton.Click += (s, e) =>
        {
            ShowLlmConfigFileSelectionDialog((configPath) =>
            {
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<LLMProviderSettings>(jsonContent);
                    if (config != null)
                    {
                        apiKeyInput.Text = config.ApiKey;
                        modelInput.Text = config.ModelName;
                        ShowToast("設定を読み込みました");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"設定読み込みエラー: {ex.Message}");
                }
            });
        };
        layout.AddView(loadButton);

        builder.SetView(layout);

        builder.SetPositiveButton("保存", (sender, e) =>
        {
            var apiKey = apiKeyInput.Text?.Trim();
            var model = modelInput.Text?.Trim() ?? "gpt-4o-mini";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ShowToast("API Keyを入力してください");
                return;
            }

            var settings = new LLMProviderSettings("openai", "https://api.openai.com/v1", model, apiKey, true);
            _settingsService?.SaveLLMProviderSettings(settings);
            InitializeLLMProvider(settings);
            ShowToast($"OpenAI設定を保存しました: {model}");
        });

        builder.SetNegativeButton("キャンセル", (sender, e) => { });
        builder.Create()?.Show();
    }

    private void ShowAzureOpenAIConfigDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("Azure OpenAI設定");

        var layout = new Android.Widget.LinearLayout(this)
        {
            Orientation = Android.Widget.Orientation.Vertical
        };
        layout.SetPadding(50, 40, 50, 10);

        // Endpoint入力
        var endpointLabel = new Android.Widget.TextView(this) { Text = "Endpoint:" };
        layout.AddView(endpointLabel);
        var endpointInput = new Android.Widget.EditText(this)
        {
            Hint = "https://your-resource.openai.azure.com",
            InputType = Android.Text.InputTypes.TextVariationUri
        };
        layout.AddView(endpointInput);

        // API Key入力
        var apiKeyLabel = new Android.Widget.TextView(this) { Text = "API Key:" };
        layout.AddView(apiKeyLabel);
        var apiKeyInput = new Android.Widget.EditText(this)
        {
            Hint = "Azure API Key",
            InputType = Android.Text.InputTypes.TextVariationPassword
        };
        layout.AddView(apiKeyInput);

        // Deployment Name入力
        var deploymentLabel = new Android.Widget.TextView(this) { Text = "Deployment Name:" };
        layout.AddView(deploymentLabel);
        var deploymentInput = new Android.Widget.EditText(this)
        {
            Hint = "gpt-4o-deployment"
        };
        layout.AddView(deploymentInput);

        // 設定ファイル読み込みボタン
        var loadButton = new Android.Widget.Button(this) { Text = "設定ファイルから読み込み" };
        loadButton.Click += (s, e) =>
        {
            ShowLlmConfigFileSelectionDialog((configPath) =>
            {
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<LLMProviderSettings>(jsonContent);
                    if (config != null)
                    {
                        endpointInput.Text = config.Endpoint;
                        apiKeyInput.Text = config.ApiKey;
                        deploymentInput.Text = config.ModelName;
                        ShowToast("設定を読み込みました");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"設定読み込みエラー: {ex.Message}");
                }
            });
        };
        layout.AddView(loadButton);

        builder.SetView(layout);

        builder.SetPositiveButton("保存", (sender, e) =>
        {
            var endpoint = endpointInput.Text?.Trim();
            var apiKey = apiKeyInput.Text?.Trim();
            var deployment = deploymentInput.Text?.Trim();

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
            {
                ShowToast("すべての項目を入力してください");
                return;
            }

            var settings = new LLMProviderSettings("azure-openai", endpoint, deployment, apiKey, true);
            _settingsService?.SaveLLMProviderSettings(settings);
            InitializeLLMProvider(settings);
            ShowToast($"Azure OpenAI設定を保存しました: {deployment}");
        });

        builder.SetNegativeButton("キャンセル", (sender, e) => { });
        builder.Create()?.Show();
    }

    private void ShowLMStudioConfigDialog(string providerId = "lm-studio-1")
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        var displayName = providerId == "lm-studio-1" ? "LM Studio 1" : "LM Studio 2";
        builder.SetTitle($"{displayName}設定");

        var layout = new Android.Widget.LinearLayout(this)
        {
            Orientation = Android.Widget.Orientation.Vertical
        };
        layout.SetPadding(50, 40, 50, 10);

        // Endpoint入力
        var endpointLabel = new Android.Widget.TextView(this) { Text = "Endpoint:" };
        layout.AddView(endpointLabel);
        var endpointInput = new Android.Widget.EditText(this)
        {
            Hint = "http://192.168.1.100:1234",
            InputType = Android.Text.InputTypes.TextVariationUri
        };
        layout.AddView(endpointInput);

        // Model入力
        var modelLabel = new Android.Widget.TextView(this) { Text = "Model:" };
        layout.AddView(modelLabel);
        var modelInput = new Android.Widget.EditText(this)
        {
            Hint = "モデル名（任意）",
            Text = "local-model"
        };
        layout.AddView(modelInput);

        // 前回の設定があれば表示
        var llmSettings = _settingsService?.LoadLLMProviderSettings();
        if (llmSettings?.Provider == providerId && !string.IsNullOrEmpty(llmSettings.Endpoint))
        {
            endpointInput.Text = llmSettings.Endpoint;
            modelInput.Text = llmSettings.ModelName;
        }

        // 設定ファイル読み込みボタン
        var loadButton = new Android.Widget.Button(this) { Text = "設定ファイルから読み込み" };
        loadButton.Click += (s, e) =>
        {
            ShowLlmConfigFileSelectionDialog((configPath) =>
            {
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<LLMProviderSettings>(jsonContent);
                    if (config != null)
                    {
                        endpointInput.Text = config.Endpoint;
                        modelInput.Text = config.ModelName;
                        ShowToast("設定を読み込みました");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"設定読み込みエラー: {ex.Message}");
                }
            });
        };
        layout.AddView(loadButton);

        builder.SetView(layout);

        builder.SetPositiveButton("保存", (sender, e) =>
        {
            var endpoint = endpointInput.Text?.Trim();
            var model = modelInput.Text?.Trim() ?? "local-model";

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                ShowToast("Endpointを入力してください");
                return;
            }

            var settings = new LLMProviderSettings(providerId, endpoint, model, null, true);
            _settingsService?.SaveLLMProviderSettings(settings);
            InitializeLLMProvider(settings);
            ShowToast($"{displayName}設定を保存しました: {endpoint}");
        });

        builder.SetNegativeButton("キャンセル", (sender, e) => { });
        builder.Create()?.Show();
    }

    private void ShowClaudeConfigDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("Claude設定");

        var layout = new Android.Widget.LinearLayout(this)
        {
            Orientation = Android.Widget.Orientation.Vertical
        };
        layout.SetPadding(50, 40, 50, 10);

        // API Key入力
        var apiKeyLabel = new Android.Widget.TextView(this) { Text = "API Key:" };
        layout.AddView(apiKeyLabel);
        var apiKeyInput = new Android.Widget.EditText(this)
        {
            Hint = "sk-ant-...",
            InputType = Android.Text.InputTypes.TextVariationPassword
        };
        layout.AddView(apiKeyInput);

        // Model入力
        var modelLabel = new Android.Widget.TextView(this) { Text = "Model:" };
        layout.AddView(modelLabel);
        var modelInput = new Android.Widget.EditText(this)
        {
            Hint = "claude-3-5-sonnet-20241022, claude-3-opus-20240229 など",
            Text = "claude-3-5-sonnet-20241022"
        };
        layout.AddView(modelInput);

        // 設定ファイル読み込みボタン
        var loadButton = new Android.Widget.Button(this) { Text = "設定ファイルから読み込み" };
        loadButton.Click += (s, e) =>
        {
            ShowLlmConfigFileSelectionDialog((configPath) =>
            {
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<LLMProviderSettings>(jsonContent);
                    if (config != null)
                    {
                        apiKeyInput.Text = config.ApiKey;
                        modelInput.Text = config.ModelName;
                        ShowToast("設定を読み込みました");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"設定読み込みエラー: {ex.Message}");
                }
            });
        };
        layout.AddView(loadButton);

        builder.SetView(layout);

        builder.SetPositiveButton("保存", (sender, e) =>
        {
            var apiKey = apiKeyInput.Text?.Trim();
            var model = modelInput.Text?.Trim() ?? "claude-3-5-sonnet-20241022";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ShowToast("API Keyを入力してください");
                return;
            }

            var settings = new LLMProviderSettings("claude", "https://api.anthropic.com/v1", model, apiKey, true);
            _settingsService?.SaveLLMProviderSettings(settings);
            InitializeLLMProvider(settings);
            ShowToast($"Claude設定を保存しました: {model}");
        });

        builder.SetNegativeButton("キャンセル", (sender, e) => { });
        builder.Create()?.Show();
    }

    private void ShowLlmConfigFileSelectionDialog(Action<string> onFileSelected)
    {
        _currentConfigFileType = "llm";
        _llmConfigCallback = onFileSelected;
        _configFilePickerLauncher?.Launch("application/json");
    }

    private void LoadAndroidDefaultStt()
    {
        Task.Run(async () =>
        {
            try
            {
                // 既存のSherpa/Azure STTサービスを停止・破棄
                _sherpaService?.StopListening();
                _sherpaService?.Dispose();
                _sherpaService = null;

                if (_azureSttService != null)
                {
                    await _azureSttService.StopListeningAsync();
                    _azureSttService?.Dispose();
                    _azureSttService = null;
                }

                RunOnUiThread(() =>
                {
                    // VoiceInputServiceは既に初期化済みなので、モデル名だけ更新
                    _currentModelName = "android-default";
                    UpdateAsrModelDisplay();
                    _statusText?.SetText("Androidデフォルト音声認識を使用", TextView.BufferType.Normal);
                    Android.Util.Log.Info("MainActivity", "Androidデフォルト音声認識に切り替え");

                    Toast.MakeText(this, "Androidデフォルト音声認識に切り替えました", ToastLength.Short)?.Show();
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    _statusText?.SetText($"切り替えエラー: {ex.Message}", TextView.BufferType.Normal);
                    Android.Util.Log.Error("MainActivity", $"Androidデフォルト音声認識切り替えエラー: {ex.Message}");
                });
            }
        });
    }

    private void ShowAzureSttConfigDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("Azure STT設定");

        // カスタムレイアウトを作成
        var layout = new Android.Widget.LinearLayout(this)
        {
            Orientation = Android.Widget.Orientation.Vertical
        };
        layout.SetPadding(50, 40, 50, 10);

        // Subscription Key入力
        var subscriptionKeyLabel = new Android.Widget.TextView(this) { Text = "Subscription Key:" };
        layout.AddView(subscriptionKeyLabel);
        var subscriptionKeyInput = new Android.Widget.EditText(this)
        {
            Hint = "Azure Subscription Key",
            InputType = Android.Text.InputTypes.TextVariationPassword
        };
        layout.AddView(subscriptionKeyInput);

        // Region入力
        var regionLabel = new Android.Widget.TextView(this) { Text = "Region:" };
        layout.AddView(regionLabel);
        var regionInput = new Android.Widget.EditText(this)
        {
            Hint = "例: japaneast",
            InputType = Android.Text.InputTypes.ClassText
        };
        layout.AddView(regionInput);

        // Language入力
        var languageLabel = new Android.Widget.TextView(this) { Text = "Language:" };
        layout.AddView(languageLabel);
        var languageInput = new Android.Widget.EditText(this)
        {
            Hint = "例: ja-JP",
            InputType = Android.Text.InputTypes.ClassText,
            Text = "ja-JP"
        };
        layout.AddView(languageInput);

        // 設定ファイル読み込みボタン
        var loadButton = new Android.Widget.Button(this) { Text = "設定ファイルから読み込み" };
        loadButton.Click += (s, e) =>
        {
            ShowAzureSttConfigFileSelectionDialog((configPath) =>
            {
                // ファイルから設定を読み込んで入力欄に反映
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<AzureSttConfig>(jsonContent);
                    if (config != null)
                    {
                        subscriptionKeyInput.Text = config.SubscriptionKey;
                        regionInput.Text = config.Region;
                        languageInput.Text = config.Language;
                        ShowToast("設定を読み込みました");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"設定読み込みエラー: {ex.Message}");
                }
            });
        };
        layout.AddView(loadButton);

        builder.SetView(layout);

        builder.SetPositiveButton("接続", (sender, e) =>
        {
            var subscriptionKey = subscriptionKeyInput.Text?.Trim();
            var region = regionInput.Text?.Trim();
            var language = languageInput.Text?.Trim() ?? "ja-JP";

            if (string.IsNullOrWhiteSpace(subscriptionKey) || string.IsNullOrWhiteSpace(region))
            {
                ShowToast("Subscription KeyとRegionを入力してください");
                return;
            }

            LoadAzureSttConfigDirect(subscriptionKey, region, language);
        });

        builder.SetNegativeButton("キャンセル", (sender, e) => { });

        var dialog = builder.Create();
        dialog?.Show();
    }

    private void ShowAzureSttConfigFileSelectionDialog(Action<string> onFileSelected)
    {
        _currentConfigFileType = "azure";
        _azureConfigCallback = onFileSelected;
        _configFilePickerLauncher?.Launch("application/json");
    }

    private void LoadAzureSttConfigDirect(string subscriptionKey, string region, string language)
    {
        Task.Run(async () =>
        {
            try
            {
                // Azure STTサービスを初期化
                _azureSttService?.Dispose();
                _azureSttService = new AzureSttService(this);

                // 設定をJSONファイルとして一時保存
                var config = new AzureSttConfig
                {
                    SubscriptionKey = subscriptionKey,
                    Region = region,
                    Language = language
                };

                var tempPath = Path.Combine(CacheDir?.AbsolutePath ?? "", "azure_stt_temp.json");
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(config);
                await File.WriteAllTextAsync(tempPath, jsonContent);

                var success = await _azureSttService.InitializeAsync(tempPath);

                RunOnUiThread(() =>
                {
                    if (success)
                    {
                        _currentModelName = "azure-stt";
                        UpdateAsrModelDisplay();

                        // 設定を保存
                        var sttSettings = new AzureSTTSettings(region, subscriptionKey, language, true);
                        _settingsService?.SaveSTTProviderSettings(sttSettings);

                        _statusText?.SetText($"Azure STT接続完了: {region}", TextView.BufferType.Normal);
                        Android.Util.Log.Info("MainActivity", $"Azure STT接続: {region}");
                        Toast.MakeText(this, $"Azure STTに接続しました", ToastLength.Short)?.Show();
                    }
                    else
                    {
                        _statusText?.SetText("Azure STT接続失敗", TextView.BufferType.Normal);
                        Android.Util.Log.Error("MainActivity", "Azure STT接続失敗");
                        Toast.MakeText(this, "Azure STT接続に失敗しました", ToastLength.Short)?.Show();
                    }
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    _statusText?.SetText($"Azure STT接続エラー: {ex.Message}", TextView.BufferType.Normal);
                    Android.Util.Log.Error("MainActivity", $"Azure STT接続エラー: {ex.Message}");
                    Toast.MakeText(this, $"エラー: {ex.Message}", ToastLength.Short)?.Show();
                });
            }
        });
    }

    private void ShowFasterWhisperServerDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("Faster Whisperサーバー設定");

        // カスタムレイアウトを作成
        var layout = new Android.Widget.LinearLayout(this)
        {
            Orientation = Android.Widget.Orientation.Vertical
        };
        layout.SetPadding(50, 40, 50, 10);

        // サーバーURL入力
        var serverUrlLabel = new Android.Widget.TextView(this) { Text = "サーバーURL:" };
        layout.AddView(serverUrlLabel);
        var serverUrlInput = new Android.Widget.EditText(this)
        {
            Hint = "例: http://192.168.1.100:8000",
            InputType = Android.Text.InputTypes.TextVariationUri
        };
        layout.AddView(serverUrlInput);

        // Language入力
        var languageLabel = new Android.Widget.TextView(this) { Text = "Language:" };
        layout.AddView(languageLabel);
        var languageInput = new Android.Widget.EditText(this)
        {
            Hint = "例: ja",
            InputType = Android.Text.InputTypes.ClassText,
            Text = _selectedLanguage
        };
        layout.AddView(languageInput);

        // 前回の設定があれば表示
        var sttSettings = _settingsService?.LoadSTTProviderSettings();
        if (sttSettings?.Provider == "faster-whisper" && !string.IsNullOrEmpty(sttSettings.Endpoint))
        {
            serverUrlInput.Text = sttSettings.Endpoint;
            if (!string.IsNullOrEmpty(sttSettings.Language))
            {
                languageInput.Text = sttSettings.Language;
            }
        }

        // 設定ファイル読み込みボタン
        var loadButton = new Android.Widget.Button(this) { Text = "設定ファイルから読み込み" };
        loadButton.Click += (s, e) =>
        {
            ShowFasterWhisperConfigFileSelectionDialog((configPath) =>
            {
                // ファイルから設定を読み込んで入力欄に反映
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<FasterWhisperSTTSettings>(jsonContent);
                    if (config != null && !string.IsNullOrEmpty(config.Endpoint))
                    {
                        serverUrlInput.Text = config.Endpoint;
                        if (!string.IsNullOrEmpty(config.Language))
                        {
                            languageInput.Text = config.Language;
                        }
                        ShowToast("設定を読み込みました");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"設定読み込みエラー: {ex.Message}");
                }
            });
        };
        layout.AddView(loadButton);

        builder.SetView(layout);

        builder.SetPositiveButton("接続", (sender, e) =>
        {
            var serverUrl = serverUrlInput.Text?.Trim();
            var language = languageInput.Text?.Trim() ?? "ja";

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                ShowToast("サーバーURLを入力してください");
                return;
            }

            _selectedLanguage = language;
            LoadFasterWhisper(serverUrl, language);
        });

        builder.SetNegativeButton("キャンセル", (sender, e) => { });

        var dialog = builder.Create();
        dialog?.Show();
    }

    private void ShowFasterWhisperConfigFileSelectionDialog(Action<string> onFileSelected)
    {
        _currentConfigFileType = "fasterwhisper";
        _fasterWhisperConfigCallback = onFileSelected;
        _configFilePickerLauncher?.Launch("application/json");
    }

    private void LoadFasterWhisper(string serverUrl, string language = "ja")
    {
        Task.Run(async () =>
        {
            try
            {
                // 既存サービスを停止
                if (_azureSttService != null)
                {
                    await _azureSttService.StopListeningAsync();
                    _azureSttService?.Dispose();
                    _azureSttService = null;
                }

                _sherpaService?.StopListening();
                _sherpaService?.Dispose();
                _sherpaService = null;

                _voiceInputService?.StopListening();

                // Faster Whisperサービスを初期化
                if (_fasterWhisperService == null)
                {
                    _fasterWhisperService = new FasterWhisperService(this);
                    _fasterWhisperService.RecognitionStarted += OnRecognitionStarted;
                    _fasterWhisperService.RecognitionStopped += OnRecognitionStopped;
                    _fasterWhisperService.RecognitionResult += OnRecognitionResult;
                    _fasterWhisperService.RecognitionError += OnRecognitionError;
                }

                var initialized = await _fasterWhisperService.InitializeAsync(serverUrl, language);

                RunOnUiThread(() =>
                {
                    if (initialized)
                    {
                        _currentModelName = "faster-whisper";
                        UpdateAsrModelDisplay();

                        // 設定を保存
                        var sttSettings = new FasterWhisperSTTSettings(serverUrl, language, true);
                        _settingsService?.SaveSTTProviderSettings(sttSettings);

                        _statusText?.SetText($"Faster Whisper接続完了: {serverUrl}", TextView.BufferType.Normal);
                        Android.Util.Log.Info("MainActivity", $"Faster Whisper接続: {serverUrl}");
                        Toast.MakeText(this, $"Faster Whisperに接続しました", ToastLength.Short)?.Show();
                    }
                    else
                    {
                        _statusText?.SetText("Faster Whisper接続失敗", TextView.BufferType.Normal);
                        Android.Util.Log.Error("MainActivity", "Faster Whisper接続失敗");
                        Toast.MakeText(this, "Faster Whisper接続に失敗しました", ToastLength.Short)?.Show();
                    }
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    _statusText?.SetText($"Faster Whisper接続エラー: {ex.Message}", TextView.BufferType.Normal);
                    Android.Util.Log.Error("MainActivity", $"Faster Whisper接続エラー: {ex.Message}");
                    Toast.MakeText(this, $"エラー: {ex.Message}", ToastLength.Short)?.Show();
                });
            }
        });
    }

    private void LoadAzureSttConfig(string configFilePath)
    {
        Task.Run(async () =>
        {
            try
            {
                // Azure STTサービスを初期化
                _azureSttService?.Dispose();
                _azureSttService = new AzureSttService(this);

                var success = await _azureSttService.InitializeAsync(configFilePath);

                RunOnUiThread(() =>
                {
                    if (success)
                    {
                        _azureSttConfigPath = configFilePath;
                        _currentModelName = "azure-stt";
                        UpdateAsrModelDisplay();
                        _statusText?.SetText($"Azure STT設定読込完了: {Path.GetFileName(configFilePath)}", TextView.BufferType.Normal);
                        Android.Util.Log.Info("MainActivity", $"Azure STT設定読込完了: {configFilePath}");
                    }
                    else
                    {
                        _statusText?.SetText("Azure STT設定読込失敗", TextView.BufferType.Normal);
                        Android.Util.Log.Error("MainActivity", "Azure STT設定読込失敗");
                    }
                });
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    _statusText?.SetText($"Azure STT設定エラー: {ex.Message}", TextView.BufferType.Normal);
                    Android.Util.Log.Error("MainActivity", $"Azure STT設定エラー: {ex.Message}");
                });
            }
        });
    }

    private void ShowLanguageSelectionDialog(string modelName)
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        var modelType = modelName.Contains("sense-voice") ? "SenseVoice" : "Whisper";
        builder.SetTitle($"{modelType} - 言語を選択");

        var languages = new string[] { "日本語", "English", "中文 (簡体)", "한국어" };
        var languageCodes = new string[] { "ja", "en", "zh", "ko" };

        int checkedItem = 0;
        for (int i = 0; i < languageCodes.Length; i++)
        {
            if (languageCodes[i] == _selectedLanguage)
            {
                checkedItem = i;
                break;
            }
        }

        AndroidX.AppCompat.App.AlertDialog? dialog = null;
        builder.SetSingleChoiceItems(languages, checkedItem, (sender, e) =>
        {
            _selectedLanguage = languageCodes[e.Which];
            dialog?.Dismiss();
            LoadModel(modelName);
        });

        builder.SetNegativeButton("キャンセル", (sender, e) =>
        {
            dialog?.Dismiss();
        });

        dialog = builder.Create();
        dialog?.Show();
    }

    private void LoadModel(string modelName)
    {
        if (_sherpaService == null)
            return;

        RunOnUiThread(() =>
        {
            _statusText!.Text = $"モデルを読み込み中: {modelName}";
            _statusText!.Visibility = ViewStates.Visible;
        });

        Task.Run(async () =>
        {
            try
            {
                Android.Util.Log.Info("MainActivity", $"モデル読み込み開始: {modelName} (Language: {_selectedLanguage})");

                // モデルパスの判定（assets内 vs 外部ストレージ）
                bool isFilePath = false;
                string resolvedModelPath = modelName;

                // 外部ストレージのパスをチェック
                string externalPath = GetExternalModelsPath(modelName);
                if (Directory.Exists(externalPath))
                {
                    isFilePath = true;
                    resolvedModelPath = externalPath;
                    Android.Util.Log.Info("MainActivity", $"外部ストレージからロード: {externalPath}");
                }
                else
                {
                    Android.Util.Log.Info("MainActivity", $"アセットからロード: {modelName}");
                }

                bool initialized = await _sherpaService.InitializeAsync(resolvedModelPath, isFilePath: isFilePath, language: _selectedLanguage);

                if (initialized)
                {
                    RunOnUiThread(() =>
                    {
                        _currentModelName = modelName;
                        UpdateAsrModelDisplay();
                        _statusText!.Visibility = ViewStates.Gone;
                        ShowToast($"モデル読み込み完了: {modelName}");
                        Android.Util.Log.Info("MainActivity", $"モデル読み込み成功: {modelName}");
                    });
                }
                else
                {
                    RunOnUiThread(() =>
                    {
                        _statusText!.Visibility = ViewStates.Gone;
                        ShowToast($"モデル読み込み失敗: {modelName}");
                        Android.Util.Log.Error("MainActivity", $"モデル読み込み失敗: {modelName}");
                    });
                }
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    _statusText!.Visibility = ViewStates.Gone;
                    ShowToast($"エラー: {ex.Message}");
                    Android.Util.Log.Error("MainActivity", $"モデル読み込みエラー: {ex.Message}");
                });
            }
        });
    }

    /// <summary>
    /// 外部ストレージのモデルパスを取得
    /// </summary>
    private string GetExternalModelsPath(string modelName)
    {
        // 複数の可能なパスを試す
        string[] possiblePaths = new[]
        {
            // アプリキャッシュディレクトリ内のmodels
            System.IO.Path.Combine(CacheDir?.AbsolutePath ?? "", "models", modelName),
            // 外部キャッシュディレクトリ
            System.IO.Path.Combine(ExternalCacheDir?.AbsolutePath ?? "", "models", modelName),
            // 公開なディレクトリ（必要に応じて）
            System.IO.Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "", "Robin", "models", modelName)
        };

        foreach (var path in possiblePaths)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                return path;
            }
        }

        return ""; // 見つからない場合は空文字列を返す
    }

    private void SetupRecyclerView()
    {
        if (_recyclerView == null || _conversationService == null)
            return;

        _messageAdapter = new MessageAdapter(_conversationService.GetMessages());
        _recyclerView.SetAdapter(_messageAdapter);
        _recyclerView.SetLayoutManager(new LinearLayoutManager(this));

        // 初期表示時に最後のメッセージまでスクロール
        _recyclerView.Post(() => ScrollToBottom());
    }

    private void SetupDrawerNavigation()
    {
        if (_navigationView == null)
            return;

        _navigationView.NavigationItemSelected += (sender, e) =>
        {
            var itemId = e.MenuItem.ItemId;

            if (itemId == Resource.Id.nav_chat)
            {
                // チャット画面 (現在の画面)
            }
            else if (itemId == Resource.Id.nav_model_management)
            {
                // 音声認識モデル選択
                ShowModelSelectionDialog();
            }
            else if (itemId == Resource.Id.nav_llm_model)
            {
                // LLMモデル選択ダイアログを表示
                ShowLlmModelSelectionDialog();
            }
            else if (itemId == Resource.Id.nav_system_prompts)
            {
                // システムプロンプト設定画面
                var intent = new Android.Content.Intent(this, typeof(SystemPromptsActivity));
                StartActivity(intent);
            }
            else if (itemId == Resource.Id.nav_about)
            {
                // バージョン情報とモデル情報
                ShowAboutDialog();
            }
            else if (itemId == Resource.Id.nav_clear_chat)
            {
                // チャット履歴をクリア
                ShowClearChatConfirmDialog();
            }
            else if (itemId == Resource.Id.nav_verbose_logging)
            {
                // 詳細ログの切り替え
                SherpaRealtimeService.VerboseLoggingEnabled = !SherpaRealtimeService.VerboseLoggingEnabled;
                var status = SherpaRealtimeService.VerboseLoggingEnabled ? "ON" : "OFF";
                e.MenuItem.SetTitle($"詳細ログ: {status}");
                ShowToast($"詳細ログ: {status}");
            }

            _drawerLayout?.CloseDrawers();
        };

        // ドロワーのスライドリスナーを設定（ノッチの表示/非表示をアニメーション）
        if (_drawerLayout != null)
        {
            _drawerLayout.AddDrawerListener(new DrawerListenerImpl(this));
        }
    }

    private void SetupMicButton()
    {
        if (_micButton == null)
            return;

        _micButton.Click += async (sender, e) =>
        {
            // ドロワーが開いていたら閉じる
            if (_drawerLayout?.IsDrawerOpen((int)GravityFlags.Start) == true)
            {
                _drawerLayout.CloseDrawer((int)GravityFlags.Start);
            }

            // Azure STT使用時
            if (_currentModelName == "azure-stt" && _azureSttService != null)
            {
                if (_azureSttService.IsListening)
                {
                    // 聴取中なら停止
                    await _azureSttService.StopListeningAsync();
                    UpdateMicButtonState();
                }
                else
                {
                    // 停止中なら開始
                    await _azureSttService.StartListeningAsync();
                    UpdateMicButtonState();
                }
            }
            // Faster Whisper使用時
            else if (_currentModelName == "faster-whisper" && _fasterWhisperService != null)
            {
                if (_fasterWhisperService.IsListening)
                {
                    // 聴取中なら停止
                    await _fasterWhisperService.StopListeningAsync();
                    UpdateMicButtonState();
                }
                else
                {
                    // 停止中なら開始
                    _fasterWhisperService.StartListening();
                    UpdateMicButtonState();
                }
            }
            // Androidデフォルト音声認識使用時
            else if (_currentModelName == "android-default" && _voiceInputService != null)
            {
                if (_voiceInputService.IsContinuousMode)
                {
                    // 継続モード中なら停止
                    _voiceInputService.StopListening();
                    UpdateMicButtonState();
                }
                else
                {
                    // 継続モードを有効にして開始
                    StartVoiceInput(enableContinuousMode: true);
                    UpdateMicButtonState();
                }
            }
            // Sherpa-ONNX使用時
            else if (_useSherpaOnnx && _sherpaService != null)
            {
                // Sherpa-ONNX使用時: 聴取状態をトグル
                if (_sherpaService.IsListening)
                {
                    // 聴取中なら停止
                    _sherpaService.StopListening();
                    UpdateMicButtonState();
                }
                else
                {
                    // 停止中なら開始
                    StartVoiceInput(enableContinuousMode: false);
                    UpdateMicButtonState();
                }
            }
            else if (_voiceInputService?.IsContinuousMode == true)
            {
                // その他: 継続モード中なら停止
                _voiceInputService.StopListening();
                UpdateMicButtonState();
            }
            else
            {
                // その他: 継続モードを有効にして開始
                StartVoiceInput(enableContinuousMode: true);
                UpdateMicButtonState();
            }
        };
    }

    private void CheckPermissions()
    {
        var permissionsToRequest = new List<string>();

        // RECORD_AUDIO権限の確認
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            permissionsToRequest.Add(Android.Manifest.Permission.RecordAudio);
        }

        // INTERNET権限の確認
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Internet) != Permission.Granted)
        {
            permissionsToRequest.Add(Android.Manifest.Permission.Internet);
        }

        // ACCESS_NETWORK_STATE権限の確認
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.AccessNetworkState) != Permission.Granted)
        {
            permissionsToRequest.Add(Android.Manifest.Permission.AccessNetworkState);
        }

        if (permissionsToRequest.Count > 0)
        {
            ActivityCompat.RequestPermissions(
                this,
                permissionsToRequest.ToArray(),
                RequestRecordAudioPermission
            );
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == RequestRecordAudioPermission)
        {
            var allGranted = grantResults.All(g => g == Permission.Granted);

            if (allGranted)
            {
                var permissionNames = string.Join(", ", permissions);
                ShowToast($"権限を許可しました: {permissionNames}");
                Android.Util.Log.Info("MainActivity", $"権限許可完了: {permissionNames}");
            }
            else
            {
                var deniedPermissions = permissions.Where((p, i) => grantResults[i] != Permission.Granted).ToArray();
                var deniedNames = string.Join(", ", deniedPermissions.Select(p => p.Split('.').Last()));
                ShowToast($"権限が必要です: {deniedNames}");
                Android.Util.Log.Warn("MainActivity", $"権限拒否: {string.Join(", ", deniedPermissions)}");
            }
        }
    }

    private void StartVoiceInput(bool enableContinuousMode = false)
    {
        // RECORD_AUDIO権限確認
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            ShowToast(GetString(Resource.String.permission_required));
            CheckPermissions();
            return;
        }

        // INTERNET権限確認
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Internet) != Permission.Granted)
        {
            ShowToast("ネットワーク通信に必要な権限がありません");
            CheckPermissions();
            return;
        }

        // モデル別の音声認識開始処理
        if (_currentModelName == "android-default" && _voiceInputService != null)
        {
            // Androidデフォルト音声認識
            _voiceInputService.StartListening(enableContinuousMode);
        }
        else if (_useSherpaOnnx && _sherpaService != null)
        {
            // Sherpa-ONNX
            _sherpaService.StartListening();
        }
        else if (_voiceInputService != null)
        {
            // フォールバック: VoiceInputService
            _voiceInputService.StartListening(enableContinuousMode);
        }
    }

    private void OnRecognitionStarted(object? sender, EventArgs e)
    {
        // 意味理解中の場合はキャンセル（新しい音声入力が始まったため）
        if (_semanticValidationCancellation != null && !_semanticValidationCancellation.IsCancellationRequested)
        {
            Android.Util.Log.Info("MainActivity", "意味理解をキャンセル（新しい音声認識が開始）");
            _semanticValidationCancellation?.Cancel();
        }

        RunOnUiThread(() =>
        {
            _statusText?.SetText(Resource.String.listening);
            _statusText!.Visibility = ViewStates.Visible;
            UpdateMicButtonState();
        });
    }

    private void OnRecognitionStopped(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            _statusText!.Visibility = ViewStates.Gone;
            UpdateMicButtonState();
        });
    }

    private async void OnRecognitionResult(object? sender, string recognizedText)
    {
        // 処理状態を更新：読み取り中
        UpdateProcessingState(RobinProcessingState.ReadingAudio, recognizedText);

        // ユーザーメッセージをConversationServiceに追加し、UIに表示
        RunOnUiThread(() =>
        {
            _conversationService?.AddUserMessage(recognizedText);
        });

        // バッファに認識結果を追加（ウォッチドッグで意味判定がトリガーされる）
        _inputBuffer?.AddRecognition(recognizedText);

        // 処理状態を更新：バッファ待機中
        UpdateProcessingState(RobinProcessingState.WaitingForBuffer);

        // 画面表示の直後に送信ボタンを有効化（意味解析待たない）
        RunOnUiThread(() =>
        {
            if (_sendButton != null)
            {
                _sendButton.Enabled = true;
            }
        });
    }

    private void OnRecognitionError(object? sender, string errorMessage)
    {
        RunOnUiThread(() =>
        {
            _statusText!.Visibility = ViewStates.Gone;
            ShowToast(errorMessage);
        });
    }

    // Sherpa-ONNXイベントハンドラー
    private void OnSherpaRecognitionStarted(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            _statusText?.SetText(Resource.String.listening);
            _statusText!.Visibility = ViewStates.Visible;
            UpdateMicButtonState();
        });
    }

    private void OnSherpaRecognitionStopped(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            _statusText!.Visibility = ViewStates.Gone;
            UpdateMicButtonState();
        });
    }

    private void OnSherpaFinalResult(object? sender, string recognizedText)
    {
        // 処理状態を更新：読み取り中
        UpdateProcessingState(RobinProcessingState.ReadingAudio, recognizedText);

        // ユーザーメッセージをConversationServiceに追加し、UIに表示
        RunOnUiThread(() =>
        {
            _conversationService?.AddUserMessage(recognizedText);
        });

        // バッファに認識結果を追加（ウォッチドッグで意味判定がトリガーされる）
        _inputBuffer?.AddRecognition(recognizedText);

        // 処理状態を更新：バッファ待機中
        UpdateProcessingState(RobinProcessingState.WaitingForBuffer);

        // 画面表示の直後に送信ボタンを有効化（意味解析待たない）
        RunOnUiThread(() =>
        {
            if (_sendButton != null)
            {
                _sendButton.Enabled = true;
            }
        });
    }

    private void OnSherpaError(object? sender, string errorMessage)
    {
        RunOnUiThread(() =>
        {
            _statusText!.Visibility = ViewStates.Gone;
            ShowToast($"Sherpa-ONNX: {errorMessage}");
        });
    }

    private void OnMessageAdded(object? sender, Message message)
    {
        RunOnUiThread(() =>
        {
            // ウェルカムメッセージを非表示にする
            if (_welcomeMessageText != null)
            {
                _welcomeMessageText.Visibility = ViewStates.Gone;
            }

            _messageAdapter?.AddMessage(message);
            ScrollToBottom();
        });
    }

    /// <summary>
    /// バッファ準備完了時のイベントハンドラー
    /// 意味妥当性の判定と LLM 処理を実行
    /// </summary>
    private async void OnInputBufferReady(object? sender, string bufferContent)
    {
        Android.Util.Log.Info("MainActivity", $"バッファ準備完了: {bufferContent}");

        // 処理状態を更新：意味判定中
        UpdateProcessingState(RobinProcessingState.EvaluatingMeaning);

        // 前回の意味判定失敗メッセージがあれば結合
        var lastInvalidMsg = _conversationService?.GetLastSemanticInvalidMessage();
        string textToValidate;
        int messageIndex;

        if (lastInvalidMsg != null)
        {
            // 前回の失敗メッセージと結合
            textToValidate = _conversationService!.MergeWithLastInvalidMessage(bufferContent);
            messageIndex = _conversationService.GetLastUserMessageIndex();

            Android.Util.Log.Info("MainActivity",
                $"前回の意味判定失敗メッセージと結合: '{lastInvalidMsg.Content}' + '{bufferContent}' = '{textToValidate}'");

            // UIを更新
            RunOnUiThread(() =>
            {
                _messageAdapter?.UpdateMessage(messageIndex, _conversationService!.GetMessages()[messageIndex]);
            });
        }
        else
        {
            // メッセージは既に OnRecognitionResult で追加されているので、indexのみ取得
            textToValidate = bufferContent;
            messageIndex = _conversationService?.GetLastUserMessageIndex() ?? -1;
        }

        // 音声認識のみモード（LLMエラー時）の場合は、意味理解をスキップ
        if (_llmOnlyTranscriptionMode)
        {
            Android.Util.Log.Info("MainActivity", "音声認識のみモード: 意味理解をスキップ");
            UpdateProcessingState(RobinProcessingState.Idle);
            return;
        }

        // 意味妥当性を判定（非同期）
        if (_semanticValidationService == null)
        {
            // 検証サービスなしの場合、意味判定をスキップ
            // 送信ボタンは既に OnRecognitionResult で有効化済み
            UpdateProcessingState(RobinProcessingState.Idle);
            return;
        }

        try
        {
            // 前回のキャンセルトークンソースをクリーンアップして、新しいものを作成
            _semanticValidationCancellation?.Cancel();  // 古いトークンを確実にキャンセル
            _semanticValidationCancellation?.Dispose();
            _semanticValidationCancellation = new CancellationTokenSource();

            var validationResult = await _semanticValidationService.ValidateAsync(textToValidate, _semanticValidationCancellation.Token);

            if (validationResult.IsSemanticValid)
            {
                // 意味が通じた場合、バッファをフラッシュ（成功したので次回結合不要）
                _inputBuffer?.FlushBuffer();

                // 意味が通じた場合、メッセージを更新して送信ボタンを有効化
                if (messageIndex >= 0)
                {
                    _conversationService?.UpdateMessageWithSemanticValidation(messageIndex, validationResult);

                    // UIを更新（色を変更）
                    RunOnUiThread(() =>
                    {
                        _messageAdapter?.UpdateMessage(messageIndex, _conversationService!.GetMessages()[messageIndex]);
                    });
                }

                // 処理状態を更新：アイドル状態
                UpdateProcessingState(RobinProcessingState.Idle);
                // 送信ボタンは既に OnRecognitionResult で有効化済み
            }
            else
            {
                // 意味が通じない場合、メッセージに検証結果を保存して次の入力を待つ
                if (messageIndex >= 0)
                {
                    _conversationService?.UpdateMessageWithSemanticValidation(messageIndex, validationResult);

                    // UIを更新（失敗状態を表示）
                    RunOnUiThread(() =>
                    {
                        _messageAdapter?.UpdateMessage(messageIndex, _conversationService!.GetMessages()[messageIndex]);
                    });
                }

                UpdateProcessingState(RobinProcessingState.Idle);

                RunOnUiThread(() =>
                {
                    _statusText!.Text = "❌ 意味が通じません。続けて話してください。";
                    _statusText!.Visibility = ViewStates.Visible;
                    // 数秒後にステータステキストを非表示
                    _statusText!.PostDelayed(() =>
                    {
                        _statusText!.Visibility = ViewStates.Gone;
                    }, 3000);
                });

                Android.Util.Log.Info("MainActivity", $"意味判定失敗: {validationResult.Feedback}（次の入力で結合して再判定します）");
            }
        }
        catch (OperationCanceledException)
        {
            // 意味解析がキャンセルされた場合（新しい音声認識が開始）
            Android.Util.Log.Info("MainActivity", "意味判定がキャンセルされました（次回入力と結合します）");

            // キャンセルされたメッセージを「意味判定失敗」としてマーク（次回結合するため）
            if (messageIndex >= 0)
            {
                var canceledResult = new SemanticValidationResult
                {
                    IsSemanticValid = false,
                    CorrectedText = textToValidate,
                    Feedback = "キャンセル（次回結合）"
                };
                _conversationService?.UpdateMessageWithSemanticValidation(messageIndex, canceledResult);

                RunOnUiThread(() =>
                {
                    _messageAdapter?.UpdateMessage(messageIndex, _conversationService!.GetMessages()[messageIndex]);
                });
            }

            UpdateProcessingState(RobinProcessingState.Idle);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"意味判定エラー: {ex.Message}");
            UpdateProcessingState(RobinProcessingState.Error, ex.Message);

            // 通常のLLMエラーと同様にエラー表示（音声認識のみモードに切り替え）
            ShowLlmError(ex.Message);
        }
    }

    /// <summary>
    /// 意味が通じたテキストを処理（LLMに送信）
    /// </summary>
    private async Task ProcessValidInput(string validatedText)
    {
        try
        {
            if (_openAIService == null || _conversationService == null)
            {
                UpdateProcessingState(RobinProcessingState.Error, "サービス未初期化");
                ShowToast("サービスが初期化されていません");
                return;
            }

            // 音声認識のみモードの場合は、LLMをスキップ
            if (_llmOnlyTranscriptionMode)
            {
                Android.Util.Log.Info("MainActivity", "音声認識のみモード: LLM処理をスキップ");
                UpdateProcessingState(RobinProcessingState.Idle);
                return;
            }

            // 処理状態を更新：入力中（LLM レスポンス待機）
            UpdateProcessingState(RobinProcessingState.WaitingForResponse);

            // 会話履歴を取得（修正済みテキストが含まれている）
            var messages = _conversationService.GetMessages();

            // 最後のユーザーメッセージの内容を確認
            var lastUserMessage = _conversationService.GetLastUserMessage();
            if (lastUserMessage != null)
            {
                Android.Util.Log.Info("MainActivity",
                    $"LLM への入力テキスト: '{lastUserMessage.Content}' " +
                    $"(修正前: '{lastUserMessage.OriginalRecognizedText ?? lastUserMessage.Content}')");

                // 修正が行われた場合
                if (lastUserMessage.SemanticValidation?.IsSemanticValid == true &&
                    lastUserMessage.OriginalRecognizedText != null &&
                    lastUserMessage.OriginalRecognizedText != lastUserMessage.Content)
                {
                    Android.Util.Log.Info("MainActivity",
                        $"テキスト修正実行: '{lastUserMessage.OriginalRecognizedText}' → '{lastUserMessage.Content}'");
                }
            }

            // LLM に送信（修正済みテキストを含む会話履歴）
            var response = await _openAIService.SendMessageAsync(messages);

            Android.Util.Log.Info("MainActivity",
                $"LLM からのレスポンス取得: {(string.IsNullOrEmpty(response) ? "なし" : response.Substring(0, Math.Min(100, response.Length)))}...");

            RunOnUiThread(() =>
            {
                _conversationService.AddAssistantMessage(response ?? "エラーが発生しました");
                // 処理完了：アイドル状態に戻す
                UpdateProcessingState(RobinProcessingState.Idle);
                ScrollToBottom();
            });
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"LLM応答エラー: {ex.Message}");
            UpdateProcessingState(RobinProcessingState.Error, ex.Message);

            // LLMエラーを表示（エラーマークを表示し、音声認識のみモードに切り替え）
            ShowLlmError(ex.Message);
        }
    }

    private void ScrollToBottom()
    {
        if (_recyclerView == null || _messageAdapter == null)
            return;

        var itemCount = _messageAdapter.ItemCount;
        if (itemCount > 0)
        {
            _recyclerView.SmoothScrollToPosition(itemCount - 1);
        }
    }

    /// <summary>
    /// 処理状態を更新してステータステキストに反映
    /// </summary>
    private void UpdateProcessingState(RobinProcessingState newState, string? details = null)
    {
        RunOnUiThread(() =>
        {
            if (newState == RobinProcessingState.Idle)
            {
                _statusText!.Visibility = ViewStates.Gone;
            }
            else
            {
                _statusText!.Text = ProcessingStateMessages.GetDetailedMessage(newState, details);
                _statusText!.Visibility = ViewStates.Visible;
            }
        });

        // ログ出力
        Android.Util.Log.Debug("MainActivity", $"処理状態: {newState} - {ProcessingStateMessages.GetMessage(newState)}");
    }

    private void ShowToast(string message)
    {
        Toast.MakeText(this, message, ToastLength.Short)?.Show();
    }

    private void ShowAboutDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("バージョン情報");

        // モデル情報を構築
        var asrModel = _currentModelName ?? "未初期化";
        var llmSettings = _settingsService?.LoadLLMProviderSettings();
        var llmModel = llmSettings?.ModelName ?? "モック版";

        var message = $"Robin v1.0.0\n\n" +
                      $"音声認識 (ASR):\n" +
                      $"{asrModel}\n\n" +
                      $"言語モデル (LLM):\n" +
                      $"{llmModel}";

        builder.SetMessage(message);
        builder.SetPositiveButton("OK", (sender, e) => { });

        var dialog = builder.Create();
        dialog?.Show();
    }

    private void ShowClearChatConfirmDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("チャット履歴をクリア");
        builder.SetMessage("本当にチャット履歴をすべて削除しますか？この操作は元に戻せません。");

        builder.SetPositiveButton("削除", (sender, e) =>
        {
            _conversationService?.ClearHistory();
            _messageAdapter?.ClearMessages();
            ShowToast("チャット履歴をクリアしました");

            // ウェルカムメッセージを表示
            ShowWelcomeMessageIfEmpty();
        });

        builder.SetNegativeButton("キャンセル", (sender, e) =>
        {
            // キャンセル - 何もしない
        });

        var dialog = builder.Create();
        dialog?.Show();
    }

    private void UpdateMicButtonState()
    {
        if (_micButton == null)
            return;

        RunOnUiThread(() =>
        {
            bool isActive = false;

            // Azure STT使用時
            if (_currentModelName == "azure-stt" && _azureSttService != null)
            {
                isActive = _azureSttService.IsListening; // 聴取中なら赤色
            }
            // Faster Whisper使用時
            else if (_currentModelName == "faster-whisper" && _fasterWhisperService != null)
            {
                isActive = _fasterWhisperService.IsListening; // 聴取中なら赤色
            }
            // Androidデフォルト音声認識使用時
            else if (_currentModelName == "android-default" && _voiceInputService != null)
            {
                isActive = _voiceInputService.IsContinuousMode; // 継続モード中なら赤色
            }
            // Sherpa-ONNX使用時
            else if (_useSherpaOnnx && _sherpaService != null)
            {
                isActive = _sherpaService.IsListening; // 聴取中なら赤色
            }
            // その他（フォールバック）
            else if (_voiceInputService != null)
            {
                isActive = _voiceInputService.IsContinuousMode; // 継続モード中なら赤色
            }

            if (isActive)
            {
                // アクティブ時: 赤色 + 停止アイコン（「押すと停止される」ことを示唆）
                _micButton.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.ParseColor("#F44336"));
                _micButton.SetImageResource(Android.Resource.Drawable.IcMediaPause);
            }
            else
            {
                // 非アクティブ時: 青色 + マイクアイコン（「押すとマイク入力が開始される」ことを示唆）
                _micButton.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.ParseColor("#2196F3"));
                // ic_btn_speak_now を ID で取得
                int micIconId = Resources!.GetIdentifier("ic_btn_speak_now", "drawable", "android");
                if (micIconId != 0)
                {
                    _micButton.SetImageResource(micIconId);
                }
                else
                {
                    // フォールバック: IcMediaPlay を使用
                    _micButton.SetImageResource(Android.Resource.Drawable.IcMediaPlay);
                }
            }
        });
    }

    private void SetupSendButton()
    {
        if (_sendButton == null)
            return;

        _sendButton.Click += (sender, e) =>
        {
            // ドロワーが開いていたら閉じる
            if (_drawerLayout?.IsDrawerOpen((int)GravityFlags.Start) == true)
            {
                _drawerLayout.CloseDrawer((int)GravityFlags.Start);
            }

            // 最新のユーザーメッセージを取得
            var messages = _conversationService?.GetMessages();
            if (messages == null || messages.Count == 0)
            {
                ShowToast("メッセージがありません");
                return;
            }

            var lastMessage = messages[messages.Count - 1];
            if (lastMessage.Role != MessageRole.User)
            {
                ShowToast("ユーザーメッセージを選択してください");
                return;
            }

            // 進行中の意味解析をキャンセル
            _semanticValidationCancellation?.Cancel();

            // 送信ボタンを即座に無効化
            RunOnUiThread(() =>
            {
                _sendButton.Enabled = false;
            });

            // LLMに送信（バックグラウンド実行）
            Task.Run(async () =>
            {
                await ProcessValidInput(lastMessage.Content);
            });
        };
    }

    private void SetupBackPressHandler()
    {
        OnBackPressedDispatcher.AddCallback(this, new BackPressedCallback(true, () =>
        {
            if (_drawerLayout?.IsDrawerOpen((int)GravityFlags.Start) == true)
            {
                _drawerLayout.CloseDrawer((int)GravityFlags.Start);
            }
            else
            {
                Finish();
            }
        }));
    }

    private class BackPressedCallback : AndroidX.Activity.OnBackPressedCallback
    {
        private readonly Action _onBackPressed;

        public BackPressedCallback(bool enabled, Action onBackPressed) : base(enabled)
        {
            _onBackPressed = onBackPressed;
        }

        public override void HandleOnBackPressed()
        {
            _onBackPressed.Invoke();
        }
    }

    private class DrawerListenerImpl : Java.Lang.Object, DrawerLayout.IDrawerListener
    {
        private readonly MainActivity _activity;

        public DrawerListenerImpl(MainActivity activity)
        {
            _activity = activity;
        }

        public void OnDrawerClosed(View drawerView)
        {
            // ドロワーが完全に閉じたらスワイプヒントを表示
            _activity._swipeHint?.Animate()
                ?.Alpha(1f)
                ?.SetDuration(200)
                ?.Start();
        }

        public void OnDrawerOpened(View drawerView)
        {
            // ドロワーが完全に開いたらスワイプヒントを非表示
            _activity._swipeHint?.Animate()
                ?.Alpha(0f)
                ?.SetDuration(200)
                ?.Start();
        }

        public void OnDrawerSlide(View drawerView, float slideOffset)
        {
            // スライド中はスワイプヒントの透明度を変更（0=閉じている、1=開いている）
            if (_activity._swipeHint != null)
            {
                _activity._swipeHint.Alpha = 1f - slideOffset;
            }
        }

        public void OnDrawerStateChanged(int newState)
        {
            // 状態変化時の処理（必要に応じて実装）
        }
    }

    private void OnSherpaInitializationProgress(object? sender, SherpaRealtimeService.InitializationProgressEventArgs e)
    {
        RunOnUiThread(() =>
        {
            if (_statusText != null)
            {
                _statusText.Text = $"{e.Status} ({e.ProgressPercentage}%)";
            }
        });
    }

    protected override void OnResume()
    {
        base.OnResume();

        // LM Studio設定画面から戻ってきた場合、LLMサービスを再初期化
        InitializeLLMService();

        // システムプロンプト設定を再適用
        ApplySystemPromptSettings();
    }

    protected override void OnDestroy()
    {
        _voiceInputService?.Dispose();
        _inputBuffer?.Dispose();
        _fasterWhisperService?.Dispose();

        if (_conversationService != null)
        {
            _conversationService.MessageAdded -= OnMessageAdded;
        }

        if (_inputBuffer != null)
        {
            _inputBuffer.BufferReady -= OnInputBufferReady;
        }

        base.OnDestroy();
    }

    /// <summary>
    /// LLMエラーアイコンがクリックされた時の処理（再接続試行）
    /// </summary>
    private void OnLlmErrorIconClick(object? sender, EventArgs e)
    {
        // エラー状態をリセット
        _llmHasError = false;
        _llmOnlyTranscriptionMode = false;

        // エラーコンテナを非表示
        RunOnUiThread(() =>
        {
            if (_llmErrorContainer != null)
            {
                _llmErrorContainer.Visibility = ViewStates.Gone;
            }
        });

        // LLMサービスを再初期化
        InitializeLLMService();

        // ユーザーに通知
        Toast.MakeText(this, "LLM接続を再試行中...", ToastLength.Short)?.Show();
    }

    /// <summary>
    /// LLMエラー状態を表示（エラーマークを表示し、音声認識のみモードに切り替え）
    /// </summary>
    private void ShowLlmError(string errorMessage)
    {
        _llmHasError = true;
        _llmOnlyTranscriptionMode = true;

        RunOnUiThread(() =>
        {
            // エラーコンテナとメッセージを表示
            if (_llmErrorContainer != null)
            {
                _llmErrorContainer.Visibility = ViewStates.Visible;
            }

            if (_llmErrorMessage != null)
            {
                // エラーメッセージを簡潔に表示（最大2行まで）
                _llmErrorMessage.Text = errorMessage.Length > 50
                    ? errorMessage.Substring(0, 47) + "..."
                    : errorMessage;
            }

            // ユーザーに通知（音声認識のみモードになったことを伝える）
            Toast.MakeText(this, $"LLM接続エラー: {errorMessage}\n音声認識のみモードに切り替わりました", ToastLength.Long)?.Show();
        });
    }

    /// <summary>
    /// モデルトグルボタンがクリックされた時の処理（LLM設定画面を開く）
    /// </summary>
    private void OnModelToggleButtonClick(object? sender, EventArgs e)
    {
        // LLMモデル選択ポップアップを開く
        ShowLlmModelSelectionDialog();
    }

    /// <summary>
    /// 現在のLLMモデル名表示を更新
    /// </summary>
    private void UpdateCurrentModelDisplay()
    {
        RunOnUiThread(() =>
        {
            if (_currentModelText != null && _settingsService != null)
            {
                var llmSettings = _settingsService.LoadLLMProviderSettings();

                string displayText;
                if (llmSettings.IsEnabled && !string.IsNullOrWhiteSpace(llmSettings.ModelName))
                {
                    var modelName = llmSettings.ModelName;

                    // モデル名を短縮表示（最大20文字）
                    if (modelName.Length > 20)
                    {
                        modelName = modelName.Substring(0, 17) + "...";
                    }

                    displayText = $"LLM: {modelName}";
                }
                else
                {
                    displayText = "LLM: オフ";
                }

                _currentModelText.Text = displayText;
            }
        });
    }

    /// <summary>
    /// ASRモデルトグルボタンがクリックされた時の処理（モデル選択ダイアログを表示）
    /// </summary>
    private void OnAsrToggleButtonClick(object? sender, EventArgs e)
    {
        ShowModelSelectionDialog();
    }

    private void OnSwipeHintClick(object? sender, EventArgs e)
    {
        _drawerLayout?.OpenDrawer((int)GravityFlags.Start);
    }

    /// <summary>
    /// 現在のASR（音声認識）モデル名表示を更新
    /// </summary>
    private void UpdateAsrModelDisplay()
    {
        RunOnUiThread(() =>
        {
            if (_currentAsrModelText != null)
            {
                var displayName = _currentModelName ?? "未初期化";
                _currentAsrModelText.Text = $"ASR: {displayName}";
            }
        });
    }

    /// <summary>
    /// URIをファイルパスに変換
    /// </summary>
    private string? GetFilePathFromUri(Android.Net.Uri uri)
    {
        Android.Util.Log.Info("MainActivity", $"URI変換開始: {uri}, Scheme: {uri.Scheme}");

        try
        {
            // ファイルスキームの場合は直接使用（最初にチェック）
            if (uri.Scheme == "file")
            {
                Android.Util.Log.Info("MainActivity", $"file:// URIを検出: {uri.Path}");
                return uri.Path;
            }

            // content:// URI の場合、データベースから取得を試みる
            if (uri.Scheme == "content")
            {
                Android.Util.Log.Info("MainActivity", $"content:// URIを検出、クエリを実行中...");
                try
                {
                    var cursor = ContentResolver?.Query(uri, null, null, null, null);
                    if (cursor != null)
                    {
                        Android.Util.Log.Info("MainActivity", $"カーソル取得成功、カラム数: {cursor.ColumnCount}");

                        // MediaStore.MediaColumns.Data を試す
                        try
                        {
#pragma warning disable CS0618, CA1422
                            int column_index = cursor.GetColumnIndexOrThrow(Android.Provider.MediaStore.MediaColumns.Data);
#pragma warning restore CS0618, CA1422
                            cursor.MoveToFirst();
                            string? filePath = cursor.GetString(column_index);
                            cursor.Close();

                            if (!string.IsNullOrEmpty(filePath))
                            {
                                Android.Util.Log.Info("MainActivity", $"ファイルパス取得成功: {filePath}");
                                return filePath;
                            }
                            else
                            {
                                Android.Util.Log.Warn("MainActivity", "MediaStore.MediaColumns.Dataから空のパスが返されました");
                            }
                        }
                        catch (Exception ex)
                        {
                            Android.Util.Log.Warn("MainActivity", $"MediaStore.MediaColumns.Dataが見つかりません: {ex.Message}");
                            cursor.Close();
                        }
                    }
                    else
                    {
                        Android.Util.Log.Warn("MainActivity", "ContentResolver.Queryがnullを返しました");
                    }
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("MainActivity", $"ContentResolver.Queryエラー: {ex.Message}");
                }
            }

            // キャッシュに一時保存して使用（フォールバック）
            Android.Util.Log.Info("MainActivity", "キャッシュへのコピーを試行中...");
            var cachedPath = CopyUriToCache(uri);
            if (!string.IsNullOrEmpty(cachedPath))
            {
                Android.Util.Log.Info("MainActivity", $"キャッシュコピー成功: {cachedPath}");
                return cachedPath;
            }

            Android.Util.Log.Error("MainActivity", "全てのURI変換方法が失敗しました");
            return null;
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"URI変換エラー(予期しない): {ex.Message}, {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// URIをキャッシュにコピー
    /// </summary>
    private string? CopyUriToCache(Android.Net.Uri uri)
    {
        Android.Util.Log.Info("MainActivity", $"URIをキャッシュにコピー中: {uri}");

        try
        {
            using var inputStream = ContentResolver?.OpenInputStream(uri);
            if (inputStream == null)
            {
                Android.Util.Log.Error("MainActivity", "OpenInputStreamがnullを返しました");
                return null;
            }

            var cacheFile = new Java.IO.File(CacheDir, "config_temp.json");
            Android.Util.Log.Info("MainActivity", $"キャッシュファイルパス: {cacheFile.AbsolutePath}");

            using var outputStream = new System.IO.FileStream(cacheFile.AbsolutePath, System.IO.FileMode.Create);
            inputStream.CopyTo(outputStream);

            Android.Util.Log.Info("MainActivity", $"キャッシュへのコピー完了: {cacheFile.AbsolutePath}");
            return cacheFile.AbsolutePath;
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"キャッシュコピーエラー: {ex.Message}, {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// ファイルピッカー結果を処理するコールバック
    /// </summary>
    private class ConfigFilePickerCallback : Java.Lang.Object, AndroidX.Activity.Result.IActivityResultCallback
    {
        private readonly MainActivity _activity;

        public ConfigFilePickerCallback(MainActivity activity)
        {
            _activity = activity;
        }

        public void OnActivityResult(Java.Lang.Object? result)
        {
            if (result is not Android.Net.Uri uri)
            {
                Android.Util.Log.Warn("MainActivity", $"ファイルピッカーが無効な結果を返しました: {result?.GetType()}");
                return;
            }

            Android.Util.Log.Info("MainActivity", $"ファイルピッカー結果: URI={uri}, Scheme={uri.Scheme}");

            try
            {
                // ContentResolverを使用してファイルを読み込む
                var filePath = _activity.GetFilePathFromUri(uri);
                if (string.IsNullOrEmpty(filePath))
                {
                    Android.Util.Log.Error("MainActivity", $"ファイルパス取得失敗: URI={uri}");
                    Toast.MakeText(_activity, "ファイルパスを取得できませんでした。ログを確認してください。", ToastLength.Long)?.Show();
                    return;
                }

                Android.Util.Log.Info("MainActivity", $"設定ファイルを選択: {filePath}");

                // ファイルタイプに応じて処理を分岐
                switch (_activity._currentConfigFileType)
                {
                    case "llm":
                        Android.Util.Log.Info("MainActivity", "LLM設定ファイルを処理中");
                        if (_activity._llmConfigCallback != null)
                        {
                            _activity._llmConfigCallback.Invoke(filePath);
                        }
                        else
                        {
                            _activity.LoadLlmConfigFromFile(filePath);
                        }
                        break;

                    case "azure":
                        Android.Util.Log.Info("MainActivity", "Azure STT設定ファイルを処理中");
                        if (_activity._azureConfigCallback != null)
                        {
                            _activity._azureConfigCallback.Invoke(filePath);
                        }
                        break;

                    case "fasterwhisper":
                        Android.Util.Log.Info("MainActivity", "Faster Whisper設定ファイルを処理中");
                        if (_activity._fasterWhisperConfigCallback != null)
                        {
                            _activity._fasterWhisperConfigCallback.Invoke(filePath);
                        }
                        break;

                    default:
                        Toast.MakeText(_activity, $"不明なファイルタイプ: {_activity._currentConfigFileType}", ToastLength.Short)?.Show();
                        Android.Util.Log.Error("MainActivity", $"不明なファイルタイプ: {_activity._currentConfigFileType}");
                        break;
                }

                // コールバックをリセット
                _activity._llmConfigCallback = null;
                _activity._azureConfigCallback = null;
                _activity._fasterWhisperConfigCallback = null;
            }
            catch (Exception ex)
            {
                Toast.MakeText(_activity, $"エラー: {ex.Message}", ToastLength.Short)?.Show();
                Android.Util.Log.Error("MainActivity", $"ファイル読み込みエラー: {ex.Message}, {ex.StackTrace}");
            }
        }
    }
}
