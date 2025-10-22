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
    private View? _swipeHint;

    private MessageAdapter? _messageAdapter;
    private ConversationService? _conversationService;
    private VoiceInputService? _voiceInputService;
    private OpenAIService? _openAIService;

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

        // モック版: APIキーなしで動作
        _openAIService = new OpenAIService("mock-api-key");
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
            else if (itemId == Resource.Id.nav_about)
            {
                // バージョン情報 (未実装)
                ShowToast("Robin v1.0.0");
            }

            _drawerLayout?.CloseDrawers();
        };

        // フッターの設定ボタンにクリックリスナーを設定
        var footerView = _navigationView.FindViewById(Resource.Id.settings_button);
        if (footerView != null)
        {
            footerView.Click += (sender, e) =>
            {
                // 設定画面 (未実装)
                ShowToast("設定画面は未実装です");
                _drawerLayout?.CloseDrawers();
            };
        }

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
            if (_voiceInputService?.IsContinuousMode == true)
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

        _voiceInputService?.StartListening(enableContinuousMode);
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
            _statusText?.SetText(Resource.String.thinking);
            _statusText!.Visibility = ViewStates.Visible;
        });

        try
        {
            // モック版を使用 (実際のAPI呼び出しなし)
            var response = await _openAIService!.SendMessageMockAsync(recognizedText);

            RunOnUiThread(() =>
            {
                _conversationService?.AddAssistantMessage(response);
                _statusText!.Visibility = ViewStates.Gone;
                ScrollToBottom();
            });
        }
        catch (Exception ex)
        {
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
        if (_micButton == null || _voiceInputService == null)
            return;

        RunOnUiThread(() =>
        {
            if (_voiceInputService.IsContinuousMode)
            {
                // 継続モード中は赤色
                _micButton.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.ParseColor("#F44336"));
            }
            else
            {
                // 通常時はプライマリカラー
                _micButton.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.ParseColor("#2196F3"));
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
