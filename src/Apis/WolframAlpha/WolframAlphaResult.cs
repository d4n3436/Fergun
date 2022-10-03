namespace Fergun.Apis.WolframAlpha;

/// <inheritdoc cref="IWolframAlphaResult"/>
public class WolframAlphaResult : IWolframAlphaResult
{
    /// <inheritdoc/>
    public WolframAlphaResultType Type { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<string> DidYouMean { get; set; } = Array.Empty<string>();

    /// <inheritdoc/>
    public IWolframAlphaFutureTopic? FutureTopic { get; set; }

    /// <inheritdoc/>
    public int? StatusCode { get; set; }

    /// <inheritdoc/>
    public string? ErrorMessage { get; set; }

    /// <inheritdoc cref="IWolframAlphaResult.Pods"/>
    public IReadOnlyCollection<WolframAlphaPod> Pods { get; set; } = Array.Empty<WolframAlphaPod>();

    /// <inheritdoc/>
    IReadOnlyCollection<IWolframAlphaPod> IWolframAlphaResult.Pods => Pods;
}