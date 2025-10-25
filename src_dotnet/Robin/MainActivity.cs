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
    private TextView? _modelNameText;
    private View? _swipeHint;

    private MessageAdapter? _messageAdapter;
    private ConversationService? _conversationService;
    private VoiceInputService? _voiceInputService;
    private SherpaRealtimeService? _sherpaService;
    private OpenAIService? _openAIService;

    // 音声認識エンジンの選択
    private bool _useSherpaOnnx = true; // true: Sherpa-ONNX, false: Android標準
    private string? _currentModelName = null; // 現在のモデル名

    // 利用可能なモデル
    private readonly string[] _availableModels = new[]
    {
        "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
        "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09"
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
    }

    private void InitializeViews()
    {
        _drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
        _navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
        _recyclerView = FindViewById<RecyclerView>(Resource.Id.chat_recycler_view);
        _micButton = FindViewById<FloatingActionButton>(Resource.Id.mic_button);
        _statusText = FindViewById<TextView>(Resource.Id.status_text);
        _modelNameText = FindViewById<TextView>(Resource.Id.model_name_text);
        _swipeHint = FindViewById(Resource.Id.swipe_hint);
    }

    private void InitializeServices()
    {
        _conversationService = new ConversationService();
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

                // アセットからモデルを初期化（優先順位順）
                string[] modelNames = new[]
                {
                    "sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01",
                    "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09"
                };

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
                        UpdateModelNameDisplay();
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
                        UpdateModelNameDisplay();
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
                    UpdateModelNameDisplay();
                    ShowToast("Sherpa初期化エラー。Android標準を使用します。");
                    _useSherpaOnnx = false;
                });
            }
        });

        // モック版: APIキーなしで動作
        _openAIService = new OpenAIService("mock-api-key");
    }

    private void UpdateModelNameDisplay()
    {
        if (_modelNameText != null && !string.IsNullOrEmpty(_currentModelName))
        {
            _modelNameText.Text = _currentModelName;
        }
    }

    private void ShowModelSelectionDialog()
    {
        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("モデルを選択");

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
        builder.SetSingleChoiceItems(_availableModels, checkedItem, (sender, e) =>
        {
            var selectedModel = _availableModels[e.Which];
            LoadModel(selectedModel);
            dialog?.Dismiss();
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
                Android.Util.Log.Info("MainActivity", $"モデル読み込み開始: {modelName}");
                bool initialized = await _sherpaService.InitializeAsync(modelName, isFilePath: false);

                if (initialized)
                {
                    RunOnUiThread(() =>
                    {
                        _currentModelName = modelName;
                        UpdateModelNameDisplay();
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
            else if (itemId == Resource.Id.nav_about)
            {
                // バージョン情報
                ShowToast("Robin v1.0.0");
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
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            ActivityCompat.RequestPermissions(
                this,
                new[] { Android.Manifest.Permission.RecordAudio },
                RequestRecordAudioPermission
            );
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == RequestRecordAudioPermission)
        {
            if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            {
                ShowToast("マイクの使用を許可しました");
            }
            else
            {
                ShowToast("マイクの使用許可が必要です");
            }
        }
    }

    private void StartVoiceInput(bool enableContinuousMode = false)
    {
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            ShowToast(GetString(Resource.String.permission_required));
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
            _statusText!.Visibility = ViewStates.Gone;
        });

        // API呼び出しはなし（オフライン版のため）
        // try
        // {
        //     // モック版を使用 (実際のAPI呼び出しなし)
        //     var response = await _openAIService!.SendMessageMockAsync(recognizedText);
        //
        //     RunOnUiThread(() =>
        //     {
        //         _conversationService?.AddAssistantMessage(response);
        //         _statusText!.Visibility = ViewStates.Gone;
        //         ScrollToBottom();
        //     });
        // }
        // catch (Exception ex)
        // {
        //     RunOnUiThread(() =>
        //     {
        //         _statusText!.Visibility = ViewStates.Gone;
        //         ShowToast($"エラー: {ex.Message}");
        //     });
        // }
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
            _statusText!.Visibility = ViewStates.Gone;
            _conversationService?.AddUserMessage(recognizedText);
        });

        // OpenAI API呼び出しはなし（オフライン版のため）
        // Task.Run(async () =>
        // {
        //     try
        //     {
        //         var response = await _openAIService?.SendMessageAsync(_conversationService?.GetMessages() ?? new List<Message>());
        //         RunOnUiThread(() =>
        //         {
        //             _conversationService?.AddAssistantMessage(response ?? "エラーが発生しました");
        //             ScrollToBottom();
        //         });
        //     }
        //     catch (Exception ex)
        //     {
        //         RunOnUiThread(() =>
        //         {
        //             ShowToast($"エラー: {ex.Message}");
        //         });
        //     }
        // });
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
