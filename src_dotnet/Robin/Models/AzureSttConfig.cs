namespace Robin.Models;

/// <summary>
/// Azure Speech-to-Text API configuration
/// </summary>
public class AzureSttConfig
{
    public required string SubscriptionKey { get; init; }
    public required string Region { get; init; }
    public string Language { get; init; } = "ja-JP";
    public string? EndpointId { get; init; } // Custom endpoint ID for custom models
    public bool EnableDictation { get; init; } = false;
    public bool EnableProfanityFilter { get; init; } = false;
    public int SpeechRecognitionLanguageAutoDetectionMode { get; init; } = 0; // 0: None, 1: AtStart, 2: Continuous
}
