using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Widget;
using AndroidX.AppCompat.App;
using Robin.Models;
using Robin.Services;

namespace Robin;

/// <summary>
/// システムプロンプト設定画面
/// Conversation プロンプットと SemanticValidation プロンプットをカスタマイズ可能
/// </summary>
[Activity(Label = "System Prompts Settings", Theme = "@style/AppTheme")]
public class SystemPromptsActivity : AppCompatActivity
{
    // UI 定数
    private const int MinPromptLength = 10;
    private const int MaxPromptLength = 5000;
    private const long MaxPromptSizeBytes = 1024 * 1024; // 1MB

    private CheckBox? _useCustomPromptsCheckbox;
    private EditText? _conversationPromptInput;
    private EditText? _semanticValidationPromptInput;
    private Button? _resetConversationButton;
    private Button? _resetSemanticValidationButton;
    private Button? _resetAllButton;
    private Button? _saveButton;
    private Button? _cancelButton;
    private SettingsService? _settingsService;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.dialog_system_prompts);

        InitializeViews();
        InitializeSettingsService();
        LoadSettings();
        RestoreScrollPositions(savedInstanceState);
        SetupEventHandlers();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        base.OnSaveInstanceState(outState);
        SaveScrollPositions(outState);
    }

    private void InitializeViews()
    {
        _useCustomPromptsCheckbox = FindViewById<CheckBox>(Resource.Id.use_custom_prompts_checkbox);
        _conversationPromptInput = FindViewById<EditText>(Resource.Id.conversation_prompt_input);
        _semanticValidationPromptInput = FindViewById<EditText>(Resource.Id.semantic_validation_prompt_input);
        _resetConversationButton = FindViewById<Button>(Resource.Id.reset_conversation_prompt_button);
        _resetSemanticValidationButton = FindViewById<Button>(Resource.Id.reset_semantic_validation_prompt_button);
        _resetAllButton = FindViewById<Button>(Resource.Id.reset_all_button);
        _saveButton = FindViewById<Button>(Resource.Id.save_button);
        _cancelButton = FindViewById<Button>(Resource.Id.cancel_button);
    }

    private void InitializeSettingsService()
    {
        if (this != null)
        {
            _settingsService = new SettingsService(this);
        }
    }

    private void LoadSettings()
    {
        if (_settingsService == null)
            return;

        var settings = _settingsService.LoadSystemPromptSettings();

        if (_useCustomPromptsCheckbox != null)
        {
            _useCustomPromptsCheckbox.Checked = settings.UseCustomPrompts;
        }

        if (_conversationPromptInput != null)
        {
            _conversationPromptInput.Text = settings.ConversationPrompt
                ?? SystemPrompts.ConversationSystemPrompt;
        }

        if (_semanticValidationPromptInput != null)
        {
            _semanticValidationPromptInput.Text = settings.SemanticValidationPrompt
                ?? SystemPrompts.SemanticValidationSystemPrompt;
        }
    }

    private void SetupEventHandlers()
    {
        if (_resetConversationButton != null)
        {
            _resetConversationButton.Click += (s, e) =>
            {
                if (_conversationPromptInput != null)
                {
                    _conversationPromptInput.Text = SystemPrompts.ConversationSystemPrompt;
                    Log.Debug("SystemPromptsActivity", "Conversation プロンプットをリセット");
                }
            };
        }

        if (_resetSemanticValidationButton != null)
        {
            _resetSemanticValidationButton.Click += (s, e) =>
            {
                if (_semanticValidationPromptInput != null)
                {
                    _semanticValidationPromptInput.Text = SystemPrompts.SemanticValidationSystemPrompt;
                    Log.Debug("SystemPromptsActivity", "SemanticValidation プロンプットをリセット");
                }
            };
        }

        if (_resetAllButton != null)
        {
            _resetAllButton.Click += (s, e) => ResetAllPrompts();
        }

        if (_saveButton != null)
        {
            _saveButton.Click += (s, e) => SaveSettings();
        }

        if (_cancelButton != null)
        {
            _cancelButton.Click += (s, e) => Finish();
        }
    }

    /// <summary>
    /// すべてのプロンプットをデフォルトにリセット
    /// </summary>
    private void ResetAllPrompts()
    {
        if (_conversationPromptInput == null || _semanticValidationPromptInput == null)
            return;

        try
        {
            _conversationPromptInput.Text = SystemPrompts.ConversationSystemPrompt;
            _semanticValidationPromptInput.Text = SystemPrompts.SemanticValidationSystemPrompt;
            _useCustomPromptsCheckbox!.Checked = false;

            Log.Info("SystemPromptsActivity", "すべてのプロンプットをデフォルトにリセット");
            Toast.MakeText(this, "すべてのプロンプットをリセットしました", ToastLength.Short)?.Show();
        }
        catch (Exception ex)
        {
            Log.Error("SystemPromptsActivity", $"リセットエラー: {ex.Message}");
            Toast.MakeText(this, $"リセットエラー: {ex.Message}", ToastLength.Long)?.Show();
        }
    }

    private void SaveSettings()
    {
        if (_settingsService == null)
            return;

        try
        {
            var useCustom = _useCustomPromptsCheckbox?.Checked ?? false;
            var conversationPrompt = _conversationPromptInput?.Text ?? "";
            var semanticValidationPrompt = _semanticValidationPromptInput?.Text ?? "";

            // プロンプットが設定されている場合、検証を実行
            if (useCustom && (!ValidatePrompt(conversationPrompt, "Conversation") ||
                             !ValidatePrompt(semanticValidationPrompt, "SemanticValidation")))
            {
                Log.Warn("SystemPromptsActivity", "プロンプット検証エラーのため、保存をスキップ");
                return; // 検証エラーがあれば保存しない
            }

            var settings = new SystemPromptSettings(
                string.IsNullOrEmpty(conversationPrompt) ? null : conversationPrompt,
                string.IsNullOrEmpty(semanticValidationPrompt) ? null : semanticValidationPrompt,
                useCustom
            );

            _settingsService.SaveSystemPromptSettings(settings);
            Log.Info("SystemPromptsActivity", $"システムプロンプト設定を保存成功（カスタムプロンプト使用: {useCustom}）");

            // 設定の変更が反映されるようにトースト表示
            Toast.MakeText(this, "システムプロンプト設定を保存しました", ToastLength.Short)?.Show();

            Finish();
        }
        catch (ArgumentException ex)
        {
            // 検証エラー（アプリケーションレベルのエラー）
            Log.Error("SystemPromptsActivity", $"設定保存検証エラー: {ex.Message}");
            Toast.MakeText(this, $"保存エラー: {ex.Message}", ToastLength.Long)?.Show();
        }
        catch (Exception ex)
        {
            // 予期しないエラー
            Log.Error("SystemPromptsActivity", $"設定保存エラー: {ex.GetType().Name} - {ex.Message}");
            Toast.MakeText(this, $"予期しないエラーが発生しました: {ex.Message}", ToastLength.Long)?.Show();
        }
    }

    /// <summary>
    /// プロンプット内容を検証
    /// </summary>
    /// <param name="prompt">検証するプロンプット文字列</param>
    /// <param name="promptType">プロンプットタイプ（ログ用）</param>
    /// <returns>検証に成功した場合 true、エラーがある場合 false</returns>
    private bool ValidatePrompt(string prompt, string promptType)
    {
        // 空またはホワイトスペースのみチェック
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ShowValidationError($"{promptType} プロンプットが空です");
            Log.Warn("SystemPromptsActivity", $"{promptType} プロンプット検証失敗: 空文字列");
            return false;
        }

        // 最小長チェック
        if (prompt.Length < MinPromptLength)
        {
            ShowValidationError($"{promptType} プロンプットが短すぎます（最小 {MinPromptLength} 文字）");
            Log.Warn("SystemPromptsActivity", $"{promptType} プロンプット検証失敗: 最小長未満 ({prompt.Length} < {MinPromptLength})");
            return false;
        }

        // 最大長チェック
        if (prompt.Length > MaxPromptLength)
        {
            ShowValidationError($"{promptType} プロンプットが長すぎます（最大 {MaxPromptLength} 文字）");
            Log.Warn("SystemPromptsActivity", $"{promptType} プロンプット検証失敗: 最大長超過 ({prompt.Length} > {MaxPromptLength})");
            return false;
        }

        // バイト数チェック（SharedPreferences のサイズ制限対応）
        var promptBytes = System.Text.Encoding.UTF8.GetByteCount(prompt);
        if (promptBytes > MaxPromptSizeBytes)
        {
            ShowValidationError($"{promptType} プロンプットのサイズが大きすぎます（最大 1MB）");
            Log.Warn("SystemPromptsActivity", $"{promptType} プロンプット検証失敗: サイズ超過 ({promptBytes} > {MaxPromptSizeBytes})");
            return false;
        }

        Log.Debug("SystemPromptsActivity", $"{promptType} プロンプット検証成功: {prompt.Length} 文字, {promptBytes} バイト");
        return true;
    }

    /// <summary>
    /// 検証エラーメッセージを表示
    /// </summary>
    private void ShowValidationError(string message)
    {
        RunOnUiThread(() =>
        {
            Toast.MakeText(this, $"エラー: {message}", ToastLength.Long)?.Show();
        });
    }

    /// <summary>
    /// EditText のスクロール位置を保存
    /// </summary>
    private void SaveScrollPositions(Bundle outState)
    {
        try
        {
            if (_conversationPromptInput != null)
            {
                var layout = _conversationPromptInput.Layout;
                if (layout != null)
                {
                    outState.PutInt("conversation_scroll_y", _conversationPromptInput.ScrollY);
                    Log.Debug("SystemPromptsActivity", $"Conversation スクロール位置を保存: {_conversationPromptInput.ScrollY}");
                }
            }

            if (_semanticValidationPromptInput != null)
            {
                var layout = _semanticValidationPromptInput.Layout;
                if (layout != null)
                {
                    outState.PutInt("semantic_scroll_y", _semanticValidationPromptInput.ScrollY);
                    Log.Debug("SystemPromptsActivity", $"SemanticValidation スクロール位置を保存: {_semanticValidationPromptInput.ScrollY}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("SystemPromptsActivity", $"スクロール位置保存エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存されたスクロール位置を復元
    /// </summary>
    private void RestoreScrollPositions(Bundle? savedInstanceState)
    {
        if (savedInstanceState == null)
            return;

        try
        {
            if (_conversationPromptInput != null && savedInstanceState.ContainsKey("conversation_scroll_y"))
            {
                var scrollY = savedInstanceState.GetInt("conversation_scroll_y", 0);
                _conversationPromptInput.SetSelection(_conversationPromptInput.Text?.Length ?? 0);
                _conversationPromptInput.ScrollTo(0, scrollY);
                Log.Debug("SystemPromptsActivity", $"Conversation スクロール位置を復元: {scrollY}");
            }

            if (_semanticValidationPromptInput != null && savedInstanceState.ContainsKey("semantic_scroll_y"))
            {
                var scrollY = savedInstanceState.GetInt("semantic_scroll_y", 0);
                _semanticValidationPromptInput.SetSelection(_semanticValidationPromptInput.Text?.Length ?? 0);
                _semanticValidationPromptInput.ScrollTo(0, scrollY);
                Log.Debug("SystemPromptsActivity", $"SemanticValidation スクロール位置を復元: {scrollY}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("SystemPromptsActivity", $"スクロール位置復元エラー: {ex.Message}");
        }
    }
}
