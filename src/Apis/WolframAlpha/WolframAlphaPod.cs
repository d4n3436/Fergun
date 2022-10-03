using System.Text.Json.Serialization;

namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaPod"/>
public record WolframAlphaPod(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("subpods")] IReadOnlyList<WolframAlphaSubPod> SubPods) : IWolframAlphaPod, IComparable<WolframAlphaPod>
{
    /// <inheritdoc/>
    IReadOnlyList<IWolframAlphaSubPod> IWolframAlphaPod.SubPods => SubPods;

    /// <inheritdoc/>
    public int CompareTo(WolframAlphaPod? other) => other is null ? 1 : Position.CompareTo(other.Position);
}