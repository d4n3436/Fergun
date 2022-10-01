using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaFutureTopic"/>
public record WolframAlphaFutureTopic(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("msg")] string Message) : IWolframAlphaFutureTopic;