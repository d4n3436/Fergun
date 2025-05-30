using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaFutureTopic"/>
[UsedImplicitly]
public record WolframAlphaFutureTopic(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("msg")] string Message) : IWolframAlphaFutureTopic;