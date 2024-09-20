using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaQuerySuggestion"/>
public record WolframAlphaQuerySuggestion(
    [property: JsonPropertyName("score")][property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] float Score,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("val")] string Value) : IWolframAlphaQuerySuggestion;