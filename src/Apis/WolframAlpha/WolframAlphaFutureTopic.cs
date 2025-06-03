using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaFutureTopic"/>
[UsedImplicitly]
public record WolframAlphaFutureTopic(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("msg")] string Message) : IWolframAlphaFutureTopic;