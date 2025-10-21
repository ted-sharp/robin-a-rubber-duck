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

        // 起動時にドロワーを開く
        _drawerLayout?.OpenDrawer((int)GravityFlags.Start);
    }

    private void InitializeViews()
    {
        _drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
        _navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
        _recyclerView = FindViewById<RecyclerView>(Resource.Id.chat_recycler_view);
        _micButton = FindViewById<FloatingActionButton>(Resource.Id.mic_button);
        _statusText = FindViewById<TextView>(Resource.Id.status_text);
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
    }

    private void SetupMicButton()
    {
        if (_micButton == null)
            return;

        _micButton.Click += (sender, e) =>
        {
            if (_voiceInputService?.IsListening == true)
            {
                _voiceInputService.StopListening();
            }
            else
            {
                StartVoiceInput();
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

    private void StartVoiceInput()
    {
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            ShowToast(GetString(Resource.String.permission_required));
            CheckPermissions();
            return;
        }

        _voiceInputService?.StartListening();
    }

    private void OnRecognitionStarted(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            _statusText?.SetText(Resource.String.listening);
            _statusText!.Visibility = ViewStates.Visible;
            // マイクボタンの色を変えて録音中を示す（アイコンは固定）
        });
    }

    private void OnRecognitionStopped(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            _statusText!.Visibility = ViewStates.Gone;
            // マイクボタンを元の状態に戻す
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
