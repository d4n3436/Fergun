using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaImageData"/>
[UsedImplicitly]
public record WolframAlphaImageData(
    [property: JsonPropertyName("src")] string SourceUrl,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("contenttype")] string ContentType) : IWolframAlphaImageData;