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

    /// <inheritdoc/>
    public IReadOnlyList<IWolframAlphaPod> Pods { get; set; } = Array.Empty<IWolframAlphaPod>();
}