using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaImageData"/>
public record WolframAlphaImageData(
    [property: JsonPropertyName("data")] byte[]? Data,
    [property: JsonPropertyName("src")] string? SourceUrl,
    [property: JsonPropertyName("imagedata")] bool IsDataPresent,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("contenttype")] string ContentType) : IWolframAlphaImageData;