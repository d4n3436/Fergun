using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaWarning"/>
public record WolframAlphaWarning([property: JsonPropertyName("text")]string Text) : IWolframAlphaWarning;