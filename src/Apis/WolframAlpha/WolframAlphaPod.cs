using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaPod"/>
public record WolframAlphaPod(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("subpods")] IReadOnlyList<WolframAlphaSubPod> SubPods) : IWolframAlphaPod
{
    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaSubPod> IWolframAlphaPod.SubPods => SubPods;
}