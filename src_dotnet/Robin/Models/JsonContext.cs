using System.Text.Json.Serialization;

namespace Robin.Models;

/// <summary>
/// JSON Source Generator context for AOT-compatible serialization
/// This enables JSON serialization in Release builds with full trimming
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
// OpenAI API models
[JsonSerializable(typeof(OpenAIRequest))]
[JsonSerializable(typeof(OpenAIResponse))]
[JsonSerializable(typeof(ApiMessage))]
[JsonSerializable(typeof(Choice))]
[JsonSerializable(typeof(List<ApiMessage>))]
[JsonSerializable(typeof(List<Choice>))]
// LLM provider settings
[JsonSerializable(typeof(LLMProviderSettings))]
[JsonSerializable(typeof(LLMProviderCollection))]
// STT provider settings
[JsonSerializable(typeof(STTProviderSettings))]
[JsonSerializable(typeof(GoogleSTTSettings))]
[JsonSerializable(typeof(AzureSTTSettings))]
[JsonSerializable(typeof(SherpaOnnxSTTSettings))]
[JsonSerializable(typeof(AndroidSTTSettings))]
[JsonSerializable(typeof(FasterWhisperSTTSettings))]
[JsonSerializable(typeof(AzureSttConfig))]
// Configuration models
[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(LLMSettings))]
[JsonSerializable(typeof(VoiceSettings))]
[JsonSerializable(typeof(STTSettings))]
[JsonSerializable(typeof(OtherSettings))]
[JsonSerializable(typeof(SystemPromptSettings))]
// Message and validation models
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(SemanticValidationResult))]
[JsonSerializable(typeof(Robin.Services.SemanticValidationService.SemanticValidationResponse))]
[JsonSerializable(typeof(MessageRole))]
[JsonSerializable(typeof(MessageDisplayState))]
[JsonSerializable(typeof(List<Message>))]
// Additional service models
[JsonSerializable(typeof(UserContextConfig))]
[JsonSerializable(typeof(Robin.Services.FasterWhisperService.FasterWhisperResponse))]
public partial class RobinJsonContext : JsonSerializerContext
{
}
