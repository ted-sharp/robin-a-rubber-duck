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
    private CheckBox? _useCustomPromptsCheckbox;
    private EditText? _conversationPromptInput;
    private EditText? _semanticValidationPromptInput;
    private Button? _resetConversationButton;
    private Button? _resetSemanticValidationButton;
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
        SetupEventHandlers();
    }

    private void InitializeViews()
    {
        _useCustomPromptsCheckbox = FindViewById<CheckBox>(Resource.Id.use_custom_prompts_checkbox);
        _conversationPromptInput = FindViewById<EditText>(Resource.Id.conversation_prompt_input);
        _semanticValidationPromptInput = FindViewById<EditText>(Resource.Id.semantic_validation_prompt_input);
        _resetConversationButton = FindViewById<Button>(Resource.Id.reset_conversation_prompt_button);
        _resetSemanticValidationButton = FindViewById<Button>(Resource.Id.reset_semantic_validation_prompt_button);
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
        if (_settingsService == null) return;

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
                }
            };
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

    private void SaveSettings()
    {
        if (_settingsService == null) return;

        var useCustom = _useCustomPromptsCheckbox?.Checked ?? false;
        var conversationPrompt = _conversationPromptInput?.Text ?? "";
        var semanticValidationPrompt = _semanticValidationPromptInput?.Text ?? "";

        var settings = new SystemPromptSettings(
            string.IsNullOrEmpty(conversationPrompt) ? null : conversationPrompt,
            string.IsNullOrEmpty(semanticValidationPrompt) ? null : semanticValidationPrompt,
            useCustom
        );

        _settingsService.SaveSystemPromptSettings(settings);
        Log.Info("SystemPromptsActivity", $"システムプロンプト設定を保存（カスタムプロンプト使用: {useCustom}）");

        // 設定の変更が反映されるようにトースト表示
        Toast.MakeText(this, "システムプロンプト設定を保存しました", ToastLength.Short)?.Show();

        Finish();
    }
}
