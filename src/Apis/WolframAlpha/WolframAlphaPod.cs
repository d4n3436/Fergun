using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaPod"/>
[UsedImplicitly]
public record WolframAlphaPod(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("subpods")] IReadOnlyList<WolframAlphaSubPod> SubPods) : IWolframAlphaPod
{
    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaSubPod> IWolframAlphaPod.SubPods => SubPods;
}