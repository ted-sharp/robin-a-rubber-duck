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
using Robin.Views;

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

    private MessageAdapter? _messageAdapter;
    private ConversationService? _conversationService;
    private VoiceInputService? _voiceInputService;
    private SherpaRealtimeService? _sherpaService;
    private OpenAIService? _openAIService;
    private SettingsService? _settingsService;

    // 音声認識エンジンの選択
    private bool _useSherpaOnnx = true; // true: Sherpa-ONNX, false: Android標準
    private string? _currentModelName = null; // 現在のモデル名
    private string _selectedLanguage = "ja"; // 選択言語（デフォルト: 日本語）

    // 利用可能なモデル
    private readonly string[] _availableModels = new[]
    {
        "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
        "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09",
        "sherpa-onnx-nemo-parakeet-tdt_ctc-0.6b-ja-35000-int8",
        "sherpa-onnx-whisper-tiny"
    };

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        InitializeViews();
        InitializeServices();
        SetupRecyclerView();
        SetupDrawerNavigation();
        SetupMicButton();
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
    }

    private void InitializeServices()
    {
        _conversationService = new ConversationService(this);
        _conversationService.MessageAdded += OnMessageAdded;

        _voiceInputService = new VoiceInputService(this);
        _voiceInputService.RecognitionStarted += OnRecognitionStarted;
        _voiceInputService.RecognitionStopped += OnRecognitionStopped;
        _voiceInputService.RecognitionResult += OnRecognitionResult;
        _voiceInputService.RecognitionError += OnRecognitionError;

        // Sherpa-ONNXサービス
        _sherpaService = new SherpaRealtimeService(this);
        _sherpaService.RecognitionStarted += OnSherpaRecognitionStarted;
        _sherpaService.RecognitionStopped += OnSherpaRecognitionStopped;
        _sherpaService.FinalResult += OnSherpaFinalResult;
        _sherpaService.Error += OnSherpaError;
        _sherpaService.InitializationProgress += OnSherpaInitializationProgress;

        // Sherpa-ONNXの非同期初期化
        Task.Run(async () =>
        {
            try
            {
                RunOnUiThread(() =>
                {
                    _statusText!.Text = "Sherpa-ONNX初期化中...";
                    _statusText!.Visibility = ViewStates.Visible;
                });

                // アセットからモデルを初期化（利用可能なモデル）
                string[] modelNames = _availableModels;

                bool initialized = false;
                string? successModel = null;

                foreach (var modelName in modelNames)
                {
                    Android.Util.Log.Info("MainActivity", $"Sherpa初期化試行: {modelName}");
                    RunOnUiThread(() =>
                    {
                        _statusText!.Text = $"初期化中: {modelName}";
                    });

                    try
                    {
                        initialized = await _sherpaService.InitializeAsync(modelName, isFilePath: false);
                        if (initialized)
                        {
                            successModel = modelName;
                            Android.Util.Log.Info("MainActivity", $"Sherpa初期化成功: {modelName}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Android.Util.Log.Warn("MainActivity", $"Sherpa初期化失敗 {modelName}: {ex.Message}");
                    }
                }

                if (initialized && successModel != null)
                {
                    RunOnUiThread(() =>
                    {
                        _statusText!.Visibility = ViewStates.Gone;
                        _currentModelName = successModel;
                        ShowToast($"Sherpa-ONNX初期化完了: {successModel}");
                    });
                }
                else
                {
                    Android.Util.Log.Error("MainActivity", "すべてのモデルで初期化失敗 - Android標準を使用");
                    RunOnUiThread(() =>
                    {
                        _statusText!.Visibility = ViewStates.Gone;
                        _currentModelName = "Android Standard (Offline)";
                        ShowToast("モデル初期化失敗。Android標準を使用します。");
                        _useSherpaOnnx = false;
                    });
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("MainActivity", $"Sherpa初期化エラー: {ex.Message}");
                RunOnUiThread(() =>
                {
                    _statusText!.Visibility = ViewStates.Gone;
                    _currentModelName = "Android Standard (Error)";
                    ShowToast("Sherpa初期化エラー。Android標準を使用します。");
                    _useSherpaOnnx = false;
                });
            }
        });

        // 設定サービス初期化
        _settingsService = new SettingsService(this);
        var llmSettings = _settingsService.LoadLLMProviderSettings();

        // LLMプロバイダー初期化
        if (llmSettings.IsEnabled && !string.IsNullOrWhiteSpace(llmSettings.ModelName))
        {
            try
            {
                if (llmSettings.Provider == "openai")
                {
                    // OpenAI API使用
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
                    // LM Studio API使用
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
        else
        {
            // モック版: APIキーなしで動作
            _openAIService = new OpenAIService("mock-api-key");
        }
    }


    private void ShowModelSelectionDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("音声認識モデルを選択");

        // モデルの表示名と説明
        var modelDisplayNames = new string[]
        {
            "Zipformer Ja (高精度日本語)",
            "SenseVoice (多言語)",
            "Nemo CTC (日本語)",
            "Whisper Tiny (多言語)"
        };

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
            var selectedModel = _availableModels[e.Which];
            // SenseVoiceまたはWhisperの場合は言語選択ダイアログを表示
            if (selectedModel.Contains("sense-voice") || selectedModel.Contains("whisper"))
            {
                dialog?.Dismiss();
                ShowLanguageSelectionDialog(selectedModel);
            }
            else
            {
                dialog?.Dismiss();
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
                // モデル管理画面
                ShowModelSelectionDialog();
            }
            else if (itemId == Resource.Id.nav_settings)
            {
                // LM Studio設定画面
                var intent = new Android.Content.Intent(this, typeof(SettingsActivity));
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

        _micButton.Click += (sender, e) =>
        {
            if (_useSherpaOnnx && _sherpaService != null)
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
                // Android標準: 継続モード中なら停止
                _voiceInputService.StopListening();
                UpdateMicButtonState();
            }
            else
            {
                // Android標準: 継続モードを有効にして開始
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

        if (_useSherpaOnnx && _sherpaService != null)
        {
            _sherpaService.StartListening();
        }
        else
        {
            _voiceInputService?.StartListening(enableContinuousMode);
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
        RunOnUiThread(() =>
        {
            _conversationService?.AddUserMessage(recognizedText);
            _statusText!.Text = "レスポンス待機中...";
            _statusText!.Visibility = ViewStates.Visible;
        });

        // LLMレスポンスを取得
        try
        {
            if (_openAIService == null || _conversationService == null)
            {
                RunOnUiThread(() =>
                {
                    _statusText!.Visibility = ViewStates.Gone;
                    ShowToast("サービスが初期化されていません");
                });
                return;
            }

            var messages = _conversationService.GetMessages();
            var response = await _openAIService.SendMessageAsync(messages);

            RunOnUiThread(() =>
            {
                _conversationService.AddAssistantMessage(response);
                _statusText!.Visibility = ViewStates.Gone;
                ScrollToBottom();
            });
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("MainActivity", $"LLM応答エラー: {ex.Message}");
            RunOnUiThread(() =>
            {
                _statusText!.Visibility = ViewStates.Gone;
                ShowToast($"エラー: {ex.Message}");
            });
        }
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
        RunOnUiThread(() =>
        {
            _conversationService?.AddUserMessage(recognizedText);
            _statusText!.Text = "レスポンス待機中...";
            _statusText!.Visibility = ViewStates.Visible;
        });

        // LLMレスポンス取得（非同期）
        Task.Run(async () =>
        {
            try
            {
                if (_openAIService == null || _conversationService == null)
                {
                    RunOnUiThread(() =>
                    {
                        _statusText!.Visibility = ViewStates.Gone;
                        ShowToast("サービスが初期化されていません");
                    });
                    return;
                }

                var messages = _conversationService.GetMessages();
                var response = await _openAIService.SendMessageAsync(messages);

                RunOnUiThread(() =>
                {
                    _conversationService.AddAssistantMessage(response ?? "エラーが発生しました");
                    _statusText!.Visibility = ViewStates.Gone;
                    ScrollToBottom();
                });
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("MainActivity", $"LLM応答エラー: {ex.Message}");
                RunOnUiThread(() =>
                {
                    _statusText!.Visibility = ViewStates.Gone;
                    ShowToast($"エラー: {ex.Message}");
                });
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

            // Sherpa-ONNX使用時
            if (_useSherpaOnnx && _sherpaService != null)
            {
                isActive = _sherpaService.IsListening; // 聴取中なら赤色
            }
            // Android標準使用時
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
                .Alpha(1f)
                .SetDuration(200)
                .Start();
        }

        public void OnDrawerOpened(View drawerView)
        {
            // ドロワーが完全に開いたらスワイプヒントを非表示
            _activity._swipeHint?.Animate()
                .Alpha(0f)
                .SetDuration(200)
                .Start();
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

    protected override void OnDestroy()
    {
        _voiceInputService?.Dispose();

        if (_conversationService != null)
        {
            _conversationService.MessageAdded -= OnMessageAdded;
        }

        base.OnDestroy();
    }
}
