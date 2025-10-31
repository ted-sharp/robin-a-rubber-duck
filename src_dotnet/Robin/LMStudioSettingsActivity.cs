using Android.App;
using Android.OS;
using Android.Util;
using Android.Widget;
using AndroidX.AppCompat.App;
using Robin.Models;
using Robin.Services;

namespace Robin;

/// <summary>
/// LM Studio設定画面
/// LM Studio 1/2のエンドポイント、モデル名、APIキーを編集可能
/// </summary>
[Activity(Label = "LM Studio Settings", Theme = "@style/AppTheme")]
public class LMStudioSettingsActivity : AppCompatActivity
{
    private Spinner? _providerSpinner;
    private EditText? _endpointInput;
    private EditText? _modelNameInput;
    private EditText? _apiKeyInput;
    private CheckBox? _enabledCheckbox;
    private Button? _testConnectionButton;
    private Button? _saveButton;
    private Button? _cancelButton;
    private SettingsService? _settingsService;

    private string _currentProviderKey = "lmStudio1"; // デフォルト: LM Studio 1

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_lmstudio_settings);

        InitializeViews();
        InitializeSettingsService();
        SetupProviderSpinner();
        LoadSettings();
        SetupEventHandlers();
    }

    private void InitializeViews()
    {
        _providerSpinner = FindViewById<Spinner>(Resource.Id.provider_spinner);
        _endpointInput = FindViewById<EditText>(Resource.Id.endpoint_input);
        _modelNameInput = FindViewById<EditText>(Resource.Id.model_name_input);
        _apiKeyInput = FindViewById<EditText>(Resource.Id.api_key_input);
        _enabledCheckbox = FindViewById<CheckBox>(Resource.Id.enabled_checkbox);
        _testConnectionButton = FindViewById<Button>(Resource.Id.test_connection_button);
        _saveButton = FindViewById<Button>(Resource.Id.save_button);
        _cancelButton = FindViewById<Button>(Resource.Id.cancel_button);
    }

    private void InitializeSettingsService()
    {
        _settingsService = new SettingsService(this);
    }

    private void SetupProviderSpinner()
    {
        if (_providerSpinner == null)
            return;

        var providers = new string[] { "LM Studio 1（自宅）", "LM Studio 2（職場）" };
        var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, providers);
        adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
        _providerSpinner.Adapter = adapter;

        _providerSpinner.ItemSelected += OnProviderSelected;
    }

    private void OnProviderSelected(object? sender, AdapterView.ItemSelectedEventArgs e)
    {
        // プロバイダーが変更されたら、設定を再読み込み
        _currentProviderKey = e.Position == 0 ? "lmStudio1" : "lmStudio2";
        LoadSettings();
        Log.Debug("LMStudioSettingsActivity", $"プロバイダーを{_currentProviderKey}に切り替え");
    }

    private void LoadSettings()
    {
        if (_settingsService == null)
            return;

        try
        {
            var collection = _settingsService.LoadLLMProviderCollection();
            var settings = _currentProviderKey == "lmStudio1" ? collection.LmStudio1 : collection.LmStudio2;

            if (settings != null)
            {
                if (_endpointInput != null)
                    _endpointInput.Text = settings.Endpoint;

                if (_modelNameInput != null)
                    _modelNameInput.Text = settings.ModelName;

                if (_apiKeyInput != null)
                    _apiKeyInput.Text = settings.ApiKey ?? "";

                if (_enabledCheckbox != null)
                    _enabledCheckbox.Checked = settings.IsEnabled;

                Log.Info("LMStudioSettingsActivity", $"{_currentProviderKey}の設定を読み込み: {settings.Endpoint}");
            }
        }
        catch (Exception ex)
        {
            Log.Error("LMStudioSettingsActivity", $"設定読み込みエラー: {ex.Message}");
            Toast.MakeText(this, $"設定読み込みエラー: {ex.Message}", ToastLength.Short)?.Show();
        }
    }

    private void SetupEventHandlers()
    {
        if (_testConnectionButton != null)
        {
            _testConnectionButton.Click += async (s, e) => await TestConnection();
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

    private async Task TestConnection()
    {
        if (_settingsService == null || _endpointInput == null || _modelNameInput == null)
            return;

        try
        {
            var endpoint = _endpointInput.Text?.Trim();
            var modelName = _modelNameInput.Text?.Trim();
            var apiKey = _apiKeyInput?.Text?.Trim();

            if (string.IsNullOrEmpty(endpoint))
            {
                Toast.MakeText(this, "エンドポイントを入力してください", ToastLength.Short)?.Show();
                return;
            }

            if (string.IsNullOrEmpty(modelName))
            {
                Toast.MakeText(this, "モデル名を入力してください", ToastLength.Short)?.Show();
                return;
            }

            Toast.MakeText(this, "接続テスト中...", ToastLength.Short)?.Show();
            Log.Info("LMStudioSettingsActivity", $"接続テスト開始: {endpoint}");

            // テスト用のOpenAIServiceを作成
            var testService = new OpenAIService(endpoint, modelName, isLMStudio: true);

            // 簡単なメッセージを送信してテスト
            var testMessages = new List<Robin.Models.Message>
            {
                new Robin.Models.Message
                {
                    Content = "Hello, this is a connection test.",
                    Role = Robin.Models.MessageRole.User
                }
            };

            var response = await testService.SendMessageAsync(testMessages);

            if (!string.IsNullOrEmpty(response))
            {
                Toast.MakeText(this, "接続成功！", ToastLength.Long)?.Show();
                Log.Info("LMStudioSettingsActivity", "接続テスト成功");
            }
            else
            {
                Toast.MakeText(this, "接続失敗: 応答が空です", ToastLength.Long)?.Show();
                Log.Warn("LMStudioSettingsActivity", "接続テスト失敗: 空の応答");
            }
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, $"接続エラー: {ex.Message}", ToastLength.Long)?.Show();
            Log.Error("LMStudioSettingsActivity", $"接続テストエラー: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        if (_settingsService == null || _endpointInput == null || _modelNameInput == null)
            return;

        try
        {
            var endpoint = _endpointInput.Text?.Trim();
            var modelName = _modelNameInput.Text?.Trim();
            var apiKey = _apiKeyInput?.Text?.Trim();
            var isEnabled = _enabledCheckbox?.Checked ?? true;

            // 入力検証
            if (string.IsNullOrEmpty(endpoint))
            {
                Toast.MakeText(this, "エンドポイントを入力してください", ToastLength.Short)?.Show();
                return;
            }

            if (string.IsNullOrEmpty(modelName))
            {
                Toast.MakeText(this, "モデル名を入力してください", ToastLength.Short)?.Show();
                return;
            }

            // 設定を保存
            var collection = _settingsService.LoadLLMProviderCollection();
            var newSettings = new LLMProviderSettings(
                "lm-studio",
                endpoint,
                modelName,
                string.IsNullOrEmpty(apiKey) ? null : apiKey,
                isEnabled
            );

            if (_currentProviderKey == "lmStudio1")
            {
                collection.LmStudio1 = newSettings;
            }
            else
            {
                collection.LmStudio2 = newSettings;
            }

            _settingsService.SaveLLMProviderCollection(collection);

            Log.Info("LMStudioSettingsActivity", $"{_currentProviderKey}の設定を保存: {endpoint}");
            Toast.MakeText(this, "設定を保存しました", ToastLength.Short)?.Show();

            Finish();
        }
        catch (Exception ex)
        {
            Log.Error("LMStudioSettingsActivity", $"設定保存エラー: {ex.Message}");
            Toast.MakeText(this, $"保存エラー: {ex.Message}", ToastLength.Long)?.Show();
        }
    }
}
