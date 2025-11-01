using Android.App;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Robin.Models;
using Robin.Services;

namespace Robin;

/// <summary>
/// 意味解析プロンプト編集画面
/// フルスクリーンでプロンプトを編集できる
/// </summary>
[Activity(Label = "意味解析プロンプト", Theme = "@style/AppTheme")]
public class SemanticValidationPromptActivity : AppCompatActivity
{
    private const int MinPromptLength = 10;
    private const int MaxPromptLength = 10000;
    private const long MaxPromptSizeBytes = 1024 * 1024; // 1MB

    private EditText? _promptInput;
    private CheckBox? _useCustomCheckbox;
    private SettingsService? _settingsService;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // レイアウトを動的に作成
        CreateLayout();

        _settingsService = new SettingsService(this);
        LoadSettings();
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
            Text = "意味解析プロンプト設定",
            TextSize = 20
        };
        title.SetTextColor(Android.Graphics.Color.Black);
        title.SetPadding(0, 0, 0, 24);
        layout.AddView(title);

        // 説明
        var description = new TextView(this)
        {
            Text = "音声認識結果の意味検証に使用するプロンプトです。",
            TextSize = 14
        };
        description.SetTextColor(Android.Graphics.Color.Gray);
        description.SetPadding(0, 0, 0, 16);
        layout.AddView(description);

        // カスタムプロンプト使用チェックボックス
        _useCustomCheckbox = new CheckBox(this)
        {
            Text = "カスタムプロンプトを使用"
        };
        _useCustomCheckbox.CheckedChange += (s, e) =>
        {
            if (_promptInput != null)
            {
                _promptInput.Enabled = e.IsChecked;
                _promptInput.Alpha = e.IsChecked ? 1.0f : 0.5f;
            }
        };
        layout.AddView(_useCustomCheckbox);

        // プロンプト入力欄（フルスクリーンで広々）
        var scrollView = new ScrollView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                0, 1.0f) // weight=1 で残りの領域を使用
        };

        _promptInput = new EditText(this)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            InputType = Android.Text.InputTypes.TextFlagMultiLine | Android.Text.InputTypes.ClassText,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
        };
        _promptInput.SetMinLines(20);
        _promptInput.Hint = "意味解析用のシステムプロンプトを入力してください";
        scrollView.AddView(_promptInput);
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

        var settings = _settingsService.LoadSystemPromptSettings();

        if (_useCustomCheckbox != null)
        {
            _useCustomCheckbox.Checked = settings.UseCustomPrompts;
        }

        if (_promptInput != null)
        {
            _promptInput.Text = settings.SemanticValidationPrompt ?? SystemPrompts.SemanticValidationSystemPrompt;
            _promptInput.Enabled = settings.UseCustomPrompts;
            _promptInput.Alpha = settings.UseCustomPrompts ? 1.0f : 0.5f;
        }
    }

    private void ResetToDefault()
    {
        if (_promptInput != null)
        {
            _promptInput.Text = SystemPrompts.SemanticValidationSystemPrompt;
            Log.Info("SemanticValidationPromptActivity", "デフォルトプロンプトにリセット");
            Toast.MakeText(this, "デフォルトにリセットしました", ToastLength.Short)?.Show();
        }
    }

    private void SaveSettings()
    {
        if (_settingsService == null)
            return;

        try
        {
            var useCustom = _useCustomCheckbox?.Checked ?? false;
            var promptText = _promptInput?.Text ?? "";

            // 検証
            if (useCustom && !ValidatePrompt(promptText))
            {
                return;
            }

            // 現在の設定を読み込んで意味解析プロンプトだけ更新
            var currentSettings = _settingsService.LoadSystemPromptSettings();
            var newSettings = new SystemPromptSettings(
                currentSettings.ConversationPrompt,
                useCustom ? promptText : null,
                useCustom
            );

            _settingsService.SaveSystemPromptSettings(newSettings);
            Log.Info("SemanticValidationPromptActivity", "意味解析プロンプトを保存");
            Toast.MakeText(this, "保存しました", ToastLength.Short)?.Show();
            Finish();
        }
        catch (Exception ex)
        {
            Log.Error("SemanticValidationPromptActivity", $"保存エラー: {ex.Message}");
            Toast.MakeText(this, $"保存エラー: {ex.Message}", ToastLength.Long)?.Show();
        }
    }

    private bool ValidatePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Toast.MakeText(this, "プロンプトが空です", ToastLength.Short)?.Show();
            return false;
        }

        if (prompt.Length < MinPromptLength)
        {
            Toast.MakeText(this, $"プロンプトが短すぎます（最小{MinPromptLength}文字）", ToastLength.Short)?.Show();
            return false;
        }

        if (prompt.Length > MaxPromptLength)
        {
            Toast.MakeText(this, $"プロンプトが長すぎます（最大{MaxPromptLength}文字）", ToastLength.Short)?.Show();
            return false;
        }

        var bytes = System.Text.Encoding.UTF8.GetByteCount(prompt);
        if (bytes > MaxPromptSizeBytes)
        {
            Toast.MakeText(this, "プロンプトのサイズが大きすぎます（最大1MB）", ToastLength.Short)?.Show();
            return false;
        }

        return true;
    }
}
