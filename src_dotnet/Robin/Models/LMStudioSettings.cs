namespace Robin.Models;

public class LMStudioSettings
{
    public string Endpoint { get; set; } = "http://192.168.0.7:1234";
    public string ModelName { get; set; } = "openai/gpt-oss-20b";
    public bool IsEnabled { get; set; } = true;

    public LMStudioSettings()
    {
    }

    public LMStudioSettings(string endpoint, string modelName, bool isEnabled = false)
    {
        Endpoint = endpoint;
        ModelName = modelName;
        IsEnabled = isEnabled;
    }
}
