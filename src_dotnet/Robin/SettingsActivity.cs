using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Widget;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.AppCompat.App;
using Robin.Models;
using Robin.Services;

namespace Robin;

[Activity(Label = "LM Studio Settings", Theme = "@style/AppTheme")]
public class SettingsActivity : AppCompatActivity
{
    private EditText? _endpointInput;
    private EditText? _modelNameInput;
    private CheckBox? _enabledCheckbox;
    private Button? _saveButton;
    private Button? _backButton;
    private Button? _importButton;
    private SettingsService? _settingsService;
    private ActivityResultLauncher? _filePickerLauncher;


    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_settings);

        InitializeViews();
        InitializeSettingsService();
        SetupFilePickerLauncher();
        LoadSettings();
        SetupEventHandlers();
    }

    private void InitializeViews()
    {
        _endpointInput = FindViewById<EditText>(Resource.Id.endpoint_input);
        _modelNameInput = FindViewById<EditText>(Resource.Id.model_name_input);
        _enabledCheckbox = FindViewById<CheckBox>(Resource.Id.lm_studio_enabled_checkbox);
        _saveButton = FindViewById<Button>(Resource.Id.save_button);
        _backButton = FindViewById<Button>(Resource.Id.settings_back_button);
        _importButton = FindViewById<Button>(Resource.Id.import_config_button);
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

        var settings = _settingsService.LoadLMStudioSettings();

        if (_endpointInput != null)
        {
            _endpointInput.Text = settings.Endpoint;
        }

        if (_modelNameInput != null)
        {
            _modelNameInput.Text = settings.ModelName;
        }

        if (_enabledCheckbox != null)
        {
            _enabledCheckbox.Checked = settings.IsEnabled;
        }
    }

    private void SetupFilePickerLauncher()
    {
        _filePickerLauncher = RegisterForActivityResult(
            new ActivityResultContracts.GetContent(),
            new ActivityResultCallback(this)
        );
    }

    private void SetupEventHandlers()
    {
        if (_saveButton != null)
        {
            _saveButton.Click += (s, e) => SaveSettings();
        }

        if (_backButton != null)
        {
            _backButton.Click += (s, e) => Finish();
        }

        if (_importButton != null)
        {
            _importButton.Click += (s, e) => OpenFilePickerDialog();
        }
    }

    private void OpenFilePickerDialog()
    {
        if (_filePickerLauncher == null)
        {
            ShowToast("ファイルピッカーが初期化されていません");
            return;
        }

        _filePickerLauncher.Launch("application/json");
    }

    public async void ImportConfigurationFile(Android.Net.Uri? uri)
    {
        if (uri == null)
        {
            ShowToast("ファイルが選択されていません");
            return;
        }

        if (_settingsService == null)
        {
            ShowToast("設定サービスが初期化されていません");
            return;
        }

        try
        {
            ShowToast("設定ファイルを読み込み中...");

            // ContentResolver を使用してファイルを読み込む
            var filePath = GetFilePathFromUri(uri);
            if (string.IsNullOrEmpty(filePath))
            {
                ShowToast("ファイルパスを取得できませんでした");
                return;
            }

            Log.Info("SettingsActivity", $"ファイルを選択: {filePath}");

            // 設定ファイルを読み込む
            var config = await _settingsService.LoadConfigurationFromFileAsync(filePath);

            if (config != null)
            {
                // 設定を適用
                _settingsService.ApplyConfiguration(config);

                // UIを更新
                LoadSettings();

                ShowToast("設定ファイルをインポートしました");
                Log.Info("SettingsActivity", "設定ファイルのインポート完了");
            }
            else
            {
                ShowToast("設定ファイルの読み込みに失敗しました");
                Log.Error("SettingsActivity", "設定ファイルの読み込みに失敗");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"エラー: {ex.Message}");
            Log.Error("SettingsActivity", $"ファイル読み込みエラー: {ex.Message}");
        }
    }

    private string? GetFilePathFromUri(Android.Net.Uri uri)
    {
        try
        {
            // content:// URI をファイルパスに変換
            var cursor = ContentResolver?.Query(uri, null, null, null, null);
            if (cursor != null)
            {
                int column_index = cursor.GetColumnIndexOrThrow(Android.Provider.MediaStore.MediaColumns.Data);
                cursor.MoveToFirst();
                string filePath = cursor.GetString(column_index);
                cursor.Close();
                return filePath;
            }

            // ファイルスキームの場合は直接使用
            if (uri.Scheme == "file")
            {
                return uri.Path;
            }

            // キャッシュに一時保存して使用
            return CopyUriToCache(uri);
        }
        catch (Exception ex)
        {
            Log.Error("SettingsActivity", $"URI変換エラー: {ex.Message}");
            return null;
        }
    }

    private string? CopyUriToCache(Android.Net.Uri uri)
    {
        try
        {
            using var inputStream = ContentResolver?.OpenInputStream(uri);
            if (inputStream == null)
                return null;

            var cacheFile = new Java.IO.File(CacheDir, "config_temp.json");
            using var outputStream = new System.IO.FileStream(cacheFile.AbsolutePath, System.IO.FileMode.Create);
            inputStream.CopyTo(outputStream);

            return cacheFile.AbsolutePath;
        }
        catch (Exception ex)
        {
            Log.Error("SettingsActivity", $"キャッシュコピーエラー: {ex.Message}");
            return null;
        }
    }

    private void SaveSettings()
    {
        if (_settingsService == null)
            return;

        var endpoint = _endpointInput?.Text ?? "http://localhost:1234";
        var modelName = _modelNameInput?.Text ?? string.Empty;
        var isEnabled = _enabledCheckbox?.Checked ?? false;

        // バリデーション
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            ShowToast("エンドポイントを入力してください");
            return;
        }

        if (isEnabled && string.IsNullOrWhiteSpace(modelName))
        {
            ShowToast("モデル名を入力してください");
            return;
        }

        // エンドポイントのバリデーション（簡易）
        if (!endpoint.StartsWith("http://") && !endpoint.StartsWith("https://"))
        {
            endpoint = "http://" + endpoint;
        }

        // 接続テスト（有効化する場合）
        if (isEnabled)
        {
            TestLMStudioConnection(endpoint, modelName);
        }
        else
        {
            SaveSettingsInternal(endpoint, modelName, isEnabled);
        }
    }

    private async void TestLMStudioConnection(string endpoint, string modelName)
    {
        ShowToast("LM Studioへの接続をテスト中...");
        try
        {
            var testTask = Task.Run(async () =>
            {
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var testUrl = endpoint.TrimEnd('/') + "/v1/chat/completions";

                Log.Info("SettingsActivity", $"接続テスト開始 - URL: {testUrl}");

                var request = new { model = modelName, messages = new[] { new { role = "user", content = "test" } }, temperature = 0.7 };
                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(testUrl, content);

                Log.Info("SettingsActivity", $"接続テスト結果 - Status: {response.StatusCode}");

                return response.IsSuccessStatusCode;
            });

            var result = await testTask;

            if (result)
            {
                ShowToast("✅ LM Studioへの接続確認成功");
                SaveSettingsInternal(endpoint, modelName, true);
            }
            else
            {
                ShowToast("❌ LM Studioへの接続に失敗しました");
            }
        }
        catch (TaskCanceledException)
        {
            ShowToast("❌ 接続タイムアウト - LM Studioが応答しません");
            Log.Error("SettingsActivity", "接続テストタイムアウト");
        }
        catch (HttpRequestException ex)
        {
            ShowToast($"❌ 接続エラー: {ex.Message}");
            Log.Error("SettingsActivity", $"接続テスト失敗: {ex.Message}");
            Log.Error("SettingsActivity", $"詳細: {ex}");
        }
        catch (Exception ex)
        {
            ShowToast($"❌ エラー: {ex.GetType().Name} - {ex.Message}");
            Log.Error("SettingsActivity", $"予期しないエラー: {ex.GetType().Name}");
            Log.Error("SettingsActivity", $"詳細: {ex}");
        }
    }

    private void SaveSettingsInternal(string endpoint, string modelName, bool isEnabled)
    {
        if (_settingsService == null)
            return;

        var settings = new LMStudioSettings(endpoint, modelName, isEnabled);
        _settingsService.SaveLMStudioSettings(settings);

        ShowToast("設定を保存しました");
        Finish();
    }

    private void ShowToast(string message)
    {
        Toast.MakeText(this, message, ToastLength.Short)?.Show();
    }
}

/// <summary>
/// ファイルピッカーの結果を処理するコールバック
/// </summary>
public class ActivityResultCallback : Java.Lang.Object, IActivityResultCallback
{
    private readonly SettingsActivity _activity;

    public ActivityResultCallback(SettingsActivity activity)
    {
        _activity = activity;
    }

    public void OnActivityResult(Java.Lang.Object? result)
    {
        if (result is Android.Net.Uri uri)
        {
            _activity.ImportConfigurationFile(uri);
        }
    }
}
