using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaSubPod"/>
[UsedImplicitly]
public record WolframAlphaSubPod(
    [property: JsonPropertyName("img")] WolframAlphaImageData Image,
    [property: JsonPropertyName("plaintext")] string PlainText,
    [property: JsonPropertyName("title")] string Title) : IWolframAlphaSubPod
{
    /// <inheritdoc/>
    IWolframAlphaImageData IWolframAlphaSubPod.Image => Image;
}