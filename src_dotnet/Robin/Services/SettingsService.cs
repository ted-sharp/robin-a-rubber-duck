using Android.Content;
using Robin.Models;

namespace Robin.Services;

public class SettingsService
{
    private readonly ISharedPreferences _preferences;
    private const string PreferencesName = "robin_settings";
    private const string LMStudioEndpointKey = "lm_studio_endpoint";
    private const string LMStudioModelNameKey = "lm_studio_model_name";
    private const string LMStudioEnabledKey = "lm_studio_enabled";

    public SettingsService(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Failed to initialize SharedPreferences");
    }

    public void SaveLMStudioSettings(LMStudioSettings settings)
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.PutString(LMStudioEndpointKey, settings.Endpoint);
            editor.PutString(LMStudioModelNameKey, settings.ModelName);
            editor.PutBoolean(LMStudioEnabledKey, settings.IsEnabled);
            editor.Commit();
        }
    }

    public LMStudioSettings LoadLMStudioSettings()
    {
        var endpoint = _preferences.GetString(LMStudioEndpointKey, "http://192.168.0.7:1234") ?? "http://192.168.0.7:1234";
        var modelName = _preferences.GetString(LMStudioModelNameKey, "openai/gpt-oss-20b") ?? "openai/gpt-oss-20b";
        var isEnabled = _preferences.GetBoolean(LMStudioEnabledKey, true);

        return new LMStudioSettings(endpoint, modelName, isEnabled);
    }

    public void ClearLMStudioSettings()
    {
        var editor = _preferences.Edit();
        if (editor != null)
        {
            editor.Remove(LMStudioEndpointKey);
            editor.Remove(LMStudioModelNameKey);
            editor.Remove(LMStudioEnabledKey);
            editor.Commit();
        }
    }
}
