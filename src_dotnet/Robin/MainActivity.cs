using Android.Content.PM;
using Android.Views;
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

    private DrawerLayout? _drawerLayout;
    private NavigationView? _navigationView;
    private RecyclerView? _recyclerView;
    private FloatingActionButton? _micButton;
    private TextView? _statusText;
    private TextView? _welcomeMessageText;
    private View? _swipeHint;
    private ImageView? _llmErrorIcon;

    private MessageAdapter? _messageAdapter;
    private ConversationService? _conversationService;
    private VoiceInputService? _voiceInputService;
    private SherpaRealtimeService? _sherpaService;
    private OpenAIService? _openAIService;
    private SettingsService? _settingsService;
    private RecognizedInputBuffer? _inputBuffer;
    private SemanticValidationService? _semanticValidationService;

    // 音声認識エンジンの選択
    private readonly bool _useSherpaOnnx = true; // true: Sherpa-ONNX, false: Android標準
    private string? _currentModelName = null; // 現在のモデル名
    private string _selectedLanguage = "ja"; // 選択言語（デフォルト: 日本語）
    private string? _azureSttConfigPath = null; // Azure STT設定ファイルパス
    private AzureSttService? _azureSttService = null; // Azure STTサービス

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

        // システムプロンプトをJSONから読み込み
        SystemPrompts.LoadFromJson(this);

        InitializeViews();
        InitializeServices();
        SetupRecyclerView();
        SetupDrawerNavigation();
        SetupMicButton();  // ← このタイミングで登録（Sherpaの初期化と並行実行）
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
        _statusText = FindViewById<TextView>(Resource.Id.status_text);
        _welcomeMessageText = FindViewById<TextView>(Resource.Id.welcome_message_text);
        _swipeHint = FindViewById(Resource.Id.swipe_hint);
        _llmErrorIcon = FindViewById<ImageView>(Resource.Id.llm_error_icon);

        // LLMエラーアイコンのタップイベント設定
        if (_llmErrorIcon != null)
        {
            _llmErrorIcon.Click += OnLlmErrorIconClick;
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
            else if (llmSettings.Provider == "lm-studio")
            {
                _openAIService = new OpenAIService(llmSettings.Endpoint, llmSettings.ModelName, isLMStudio: true);
                Android.Util.Log.Info("MainActivity", $"LM Studio初期化: {llmSettings.Endpoint}");
                ShowToast($"LM Studio接続: {llmSettings.ModelName}");
            }
            else
            {
                throw new InvalidOperationException($"未サポートのプロバイダー: {llmSettings.Provider}");
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"LLM初期化失敗: {ex.Message}");
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

            if (promptSettings.UseCustomPrompts)
            {
                // カスタムプロンプトを使用
                if (!string.IsNullOrEmpty(promptSettings.ConversationPrompt))
                {
                    _openAIService.SetSystemPrompt(promptSettings.ConversationPrompt);
                    Android.Util.Log.Info("MainActivity", "カスタム Conversation プロンプトを適用");
                }

                // SemanticValidationService もカスタムプロンプトを認識するように設定
                // （SemanticValidationService 内部で自動的に処理される）
            }
            else
            {
                // デフォルトプロンプトを使用
                _openAIService.SetSystemPrompt(SystemPrompts.PromptType.Conversation);
                Android.Util.Log.Info("MainActivity", "デフォルト Conversation プロンプトを適用");
            }
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
        // Androidデフォルトとクラウドサービスは常に利用可能
        if (modelName == "android-default" || modelName == "azure-stt")
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
            // Azure STTの場合は設定ファイル選択ダイアログを表示
            else if (selectedModel == "azure-stt")
            {
                ShowAzureSttConfigFileSelectionDialog();
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

        // LLMモデルの選択肢
        var llmModelNames = new string[]
        {
            "LM Studio 1（自宅）",
            "LM Studio 2（職場）",
            "LM Studio 設定を編集...",
            "設定ファイルから読み込み..."
        };

        AndroidX.AppCompat.App.AlertDialog? dialog = null;
        builder.SetItems(llmModelNames, (sender, e) =>
        {
            dialog?.Dismiss();

            switch (e.Which)
            {
                case 0: // LM Studio 1
                    LoadLlmProvider("lmStudio1");
                    break;
                case 1: // LM Studio 2
                    LoadLlmProvider("lmStudio2");
                    break;
                case 2: // LM Studio設定画面
                    var intent = new Android.Content.Intent(this, typeof(LMStudioSettingsActivity));
                    StartActivity(intent);
                    break;
                case 3: // ファイル選択
                    ShowLlmConfigFileSelectionDialog();
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
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("LLM設定ファイルを選択");

        // /sdcard/Download/ ディレクトリから .json ファイルを検索
        var downloadsPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;

        if (string.IsNullOrEmpty(downloadsPath) || !Directory.Exists(downloadsPath))
        {
            var errorBuilder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            errorBuilder.SetTitle("エラー");
            errorBuilder.SetMessage("Downloadフォルダにアクセスできません");
            errorBuilder.SetPositiveButton("OK", (s, e) => { });
            errorBuilder.Show();
            return;
        }

        var jsonFiles = Directory.GetFiles(downloadsPath, "*llm*.json")
            .Concat(Directory.GetFiles(downloadsPath, "*lmstudio*.json"))
            .Concat(Directory.GetFiles(downloadsPath, "*.json"))
            .Distinct()
            .ToArray();

        if (jsonFiles.Length == 0)
        {
            var errorBuilder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            errorBuilder.SetTitle("設定ファイルが見つかりません");
            errorBuilder.SetMessage($"Downloadフォルダに .json ファイルが見つかりません。\n\nパス: {downloadsPath}");
            errorBuilder.SetPositiveButton("OK", (s, e) => { });
            errorBuilder.Show();
            return;
        }

        var fileNames = jsonFiles.Select(f => Path.GetFileName(f)).ToArray();

        AndroidX.AppCompat.App.AlertDialog? dialog = null;
        builder.SetItems(fileNames, (sender, e) =>
        {
            var selectedFilePath = jsonFiles[e.Which];
            dialog?.Dismiss();
            LoadLlmConfigFromFile(selectedFilePath);
        });

        builder.SetNegativeButton("キャンセル", (sender, e) =>
        {
            dialog?.Dismiss();
        });

        dialog = builder.Create();
        dialog?.Show();
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

    private void ShowAzureSttConfigFileSelectionDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("Azure STT設定ファイルを選択");

        // /sdcard/Download/ ディレクトリから .json ファイルを検索
        var downloadsPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;

        if (string.IsNullOrEmpty(downloadsPath) || !Directory.Exists(downloadsPath))
        {
            var errorBuilder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            errorBuilder.SetTitle("エラー");
            errorBuilder.SetMessage("Downloadフォルダにアクセスできません");
            errorBuilder.SetPositiveButton("OK", (s, e) => { });
            errorBuilder.Show();
            return;
        }

        var jsonFiles = Directory.GetFiles(downloadsPath, "*.json");

        if (jsonFiles.Length == 0)
        {
            var errorBuilder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            errorBuilder.SetTitle("設定ファイルが見つかりません");
            errorBuilder.SetMessage($"Downloadフォルダに .json ファイルが見つかりません。\n\nパス: {downloadsPath}");
            errorBuilder.SetPositiveButton("OK", (s, e) => { });
            errorBuilder.Show();
            return;
        }

        var fileNames = jsonFiles.Select(f => Path.GetFileName(f)).ToArray();

        AndroidX.AppCompat.App.AlertDialog? dialog = null;
        builder.SetItems(fileNames, (sender, e) =>
        {
            var selectedFilePath = jsonFiles[e.Which];
            dialog?.Dismiss();
            LoadAzureSttConfig(selectedFilePath);
        });

        builder.SetNegativeButton("キャンセル", (sender, e) =>
        {
            dialog?.Dismiss();
        });

        dialog = builder.Create();
        dialog?.Show();
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
                // LLM設定画面を直接開く
                var intent = new Android.Content.Intent(this, typeof(LMStudioSettingsActivity));
                StartActivity(intent);
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

        // バッファに認識結果を追加（ウォッチドッグで処理される）
        _inputBuffer?.AddRecognition(recognizedText);

        // 処理状態を更新：バッファ待機中
        UpdateProcessingState(RobinProcessingState.WaitingForBuffer);
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

        // バッファに認識結果を追加（ウォッチドッグで処理される）
        _inputBuffer?.AddRecognition(recognizedText);

        // 処理状態を更新：バッファ待機中
        UpdateProcessingState(RobinProcessingState.WaitingForBuffer);
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
            // 新規メッセージを追加（生の認識結果）
            textToValidate = bufferContent;
            RunOnUiThread(() =>
            {
                _conversationService?.AddUserMessage(bufferContent);
            });
            messageIndex = _conversationService?.GetLastUserMessageIndex() ?? -1;
        }

        // バッファをフラッシュ
        _inputBuffer?.FlushBuffer();

        // 意味妥当性を判定（非同期）
        if (_semanticValidationService == null)
        {
            // 検証サービスなしで直接処理
            await ProcessValidInput(textToValidate);
            return;
        }

        try
        {
            var validationResult = await _semanticValidationService.ValidateAsync(textToValidate);

            if (validationResult.IsSemanticValid)
            {
                // 処理状態を更新：テキスト確認中
                UpdateProcessingState(RobinProcessingState.ProcessingText);

                // 意味が通じた場合、メッセージを更新して LLM処理へ
                if (messageIndex >= 0)
                {
                    _conversationService?.UpdateMessageWithSemanticValidation(messageIndex, validationResult);

                    // UIを更新（色を変更）
                    RunOnUiThread(() =>
                    {
                        _messageAdapter?.UpdateMessage(messageIndex, _conversationService!.GetMessages()[messageIndex]);
                    });
                }

                await ProcessValidInput(validationResult.CorrectedText ?? textToValidate);
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
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"意味判定エラー: {ex.Message}");
            UpdateProcessingState(RobinProcessingState.Error, "判定エラー");

            RunOnUiThread(() =>
            {
                _statusText!.PostDelayed(() =>
                {
                    UpdateProcessingState(RobinProcessingState.Idle);
                }, 2000);
            });

            // エラー時は元のテキストで処理
            await ProcessValidInput(textToValidate);
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

        // エラーアイコンを非表示
        RunOnUiThread(() =>
        {
            if (_llmErrorIcon != null)
            {
                _llmErrorIcon.Visibility = ViewStates.Gone;
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
            // エラーアイコンを表示
            if (_llmErrorIcon != null)
            {
                _llmErrorIcon.Visibility = ViewStates.Visible;
            }

            // ユーザーに通知（音声認識のみモードになったことを伝える）
            Toast.MakeText(this, $"LLM接続エラー: {errorMessage}\n音声認識のみモードに切り替わりました", ToastLength.Long)?.Show();
        });
    }
}
