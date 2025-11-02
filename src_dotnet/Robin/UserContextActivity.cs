using Android.App;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Robin.Services;
using System.Text.Json;
using Robin.Models;

namespace Robin;

/// <summary>
/// ユーザーコンテキスト編集画面
/// ユーザーのバックグラウンド情報を入力してプロンプトに反映
/// </summary>
[Activity(Label = "ユーザーコンテキスト", Theme = "@style/AppTheme")]
public class UserContextActivity : AppCompatActivity
{
    private const int MaxContextLength = 2000;
    private const long MaxContextSizeBytes = 512 * 1024; // 512KB

    private EditText? _contextInput;
    private CheckBox? _useContextCheckbox;
    private SettingsService? _settingsService;
    private UserContextConfig? _userContextConfig;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _settingsService = new SettingsService(this);
        LoadUserContextConfig();
        CreateLayout();
        LoadSettings();
    }

    /// <summary>
    /// JSONファイルからユーザーコンテキスト設定を読み込む
    /// </summary>
    private void LoadUserContextConfig()
    {
        try
        {
            // アセットフォルダから user_context.json を読み込む
            using var stream = Assets?.Open("Resources/raw/user_context.json");
            if (stream == null)
            {
                Log.Error("UserContextActivity", "user_context.json not found in Assets");
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            _userContextConfig = JsonSerializer.Deserialize(json, Robin.Models.RobinJsonContext.Default.UserContextConfig);

            if (_userContextConfig != null)
            {
                Log.Info("UserContextActivity", $"Successfully loaded user context config (DefaultContext: {_userContextConfig.DefaultContext?.Length ?? 0} chars)");
            }
            else
            {
                Log.Error("UserContextActivity", "Failed to deserialize user_context.json");
            }
        }
        catch (Exception ex)
        {
            Log.Error("UserContextActivity", $"Error loading user_context.json: {ex.Message}");
            _userContextConfig = null;
        }
    }

    private void CreateLayout()
    {
        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        layout.SetPadding(32, 32, 32, 32);

        // タイトル
        var title = new TextView(this)
        {
            Text = "ユーザーコンテキスト設定",
            TextSize = 20
        };
        title.SetTextColor(Android.Graphics.Color.Black);
        title.SetPadding(0, 0, 0, 16);
        layout.AddView(title);

        // 説明
        var description = new TextView(this)
        {
            Text = "あなたのバックグラウンド、スキル、現在の状況など、AIが適切なサポートをするために役立つ情報を入力してください。",
            TextSize = 14
        };
        description.SetTextColor(Android.Graphics.Color.Gray);
        description.SetPadding(0, 0, 0, 16);
        layout.AddView(description);

        // コンテキスト使用チェックボックス
        _useContextCheckbox = new CheckBox(this)
        {
            Text = "ユーザーコンテキストを使用"
        };
        _useContextCheckbox.CheckedChange += (s, e) =>
        {
            if (_contextInput != null)
            {
                _contextInput.Enabled = e.IsChecked;
                _contextInput.Alpha = e.IsChecked ? 1.0f : 0.5f;
            }
        };
        layout.AddView(_useContextCheckbox);

        // コンテキスト入力欄（フルスクリーンで広々）
        var scrollView = new ScrollView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                0, 1.0f)
        };

        _contextInput = new EditText(this)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            InputType = Android.Text.InputTypes.TextFlagMultiLine | Android.Text.InputTypes.ClassText,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
        };
        _contextInput.SetMinLines(15);
        _contextInput.Hint = "あなたのバックグラウンド、スキル、現在の状況などを自由に入力してください";
        scrollView.AddView(_contextInput);
        layout.AddView(scrollView);

        // ボタン領域
        var buttonLayout = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
        };
        buttonLayout.SetPadding(0, 24, 0, 0);

        var resetButton = new Button(this) { Text = "リセット" };
        resetButton.Click += (s, e) => ResetToDefault();
        buttonLayout.AddView(resetButton, new LinearLayout.LayoutParams(
            0, ViewGroup.LayoutParams.WrapContent, 1.0f));

        var cancelButton = new Button(this) { Text = "キャンセル" };
        cancelButton.Click += (s, e) => Finish();
        buttonLayout.AddView(cancelButton, new LinearLayout.LayoutParams(
            0, ViewGroup.LayoutParams.WrapContent, 1.0f));

        var saveButton = new Button(this) { Text = "保存" };
        saveButton.Click += (s, e) => SaveSettings();
        buttonLayout.AddView(saveButton, new LinearLayout.LayoutParams(
            0, ViewGroup.LayoutParams.WrapContent, 1.0f));

        layout.AddView(buttonLayout);

        SetContentView(layout);
    }

    private void LoadSettings()
    {
        if (_settingsService == null)
            return;

        var context = _settingsService.LoadUserContext();
        var useContext = _settingsService.LoadUseUserContext();

        // 初回は初期値を使用
        if (string.IsNullOrEmpty(context) && _userContextConfig?.DefaultContext != null)
        {
            context = _userContextConfig.DefaultContext;
        }

        if (_useContextCheckbox != null)
        {
            _useContextCheckbox.Checked = useContext;
        }

        if (_contextInput != null)
        {
            _contextInput.Text = context;
            _contextInput.Enabled = useContext;
            _contextInput.Alpha = useContext ? 1.0f : 0.5f;
        }
    }

    /// <summary>
    /// ユーザーコンテキストをデフォルト値にリセット
    /// </summary>
    private void ResetToDefault()
    {
        if (_contextInput == null)
        {
            Log.Error("UserContextActivity", "ContextInput is null");
            Toast.MakeText(this, "エラー: テキスト入力欄が見つかりません", ToastLength.Short)?.Show();
            return;
        }

        if (_userContextConfig?.DefaultContext == null)
        {
            Log.Error("UserContextActivity", "DefaultContext is null");
            Toast.MakeText(this, "エラー: デフォルト値が読み込まれていません", ToastLength.Short)?.Show();
            return;
        }

        _contextInput.Text = _userContextConfig.DefaultContext;
        Log.Info("UserContextActivity", $"デフォルト値にリセット (長さ: {_userContextConfig.DefaultContext.Length})");
        Toast.MakeText(this, "デフォルト値にリセットしました", ToastLength.Short)?.Show();
    }

    private void SaveSettings()
    {
        if (_settingsService == null)
            return;

        try
        {
            var useContext = _useContextCheckbox?.Checked ?? false;
            var contextText = _contextInput?.Text ?? "";

            // 検証
            if (useContext && !string.IsNullOrWhiteSpace(contextText))
            {
                if (!ValidateContext(contextText))
                {
                    return;
                }
            }

            _settingsService.SaveUserContext(contextText);
            _settingsService.SaveUseUserContext(useContext);

            Log.Info("UserContextActivity", $"ユーザーコンテキストを保存 (使用: {useContext}, 長さ: {contextText.Length})");
            Toast.MakeText(this, "保存しました", ToastLength.Short)?.Show();
            Finish();
        }
        catch (Exception ex)
        {
            Log.Error("UserContextActivity", $"保存エラー: {ex.Message}");
            Toast.MakeText(this, $"保存エラー: {ex.Message}", ToastLength.Long)?.Show();
        }
    }

    private bool ValidateContext(string context)
    {
        if (context.Length > MaxContextLength)
        {
            Toast.MakeText(this, $"コンテキストが長すぎます（最大{MaxContextLength}文字）", ToastLength.Short)?.Show();
            return false;
        }

        var bytes = System.Text.Encoding.UTF8.GetByteCount(context);
        if (bytes > MaxContextSizeBytes)
        {
            Toast.MakeText(this, "コンテキストのサイズが大きすぎます（最大512KB）", ToastLength.Short)?.Show();
            return false;
        }

        return true;
    }
}

/// <summary>
/// ユーザーコンテキスト設定ファイルのモデル
/// </summary>
public class UserContextConfig
{
    /// <summary>
    /// デフォルトのユーザーコンテキスト
    /// </summary>
    public string? DefaultContext { get; set; }
}
