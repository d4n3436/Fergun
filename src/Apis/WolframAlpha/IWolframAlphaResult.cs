namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a result from WolframAlpha's API.
/// </summary>
public interface IWolframAlphaResult
{
    /// <summary>
    /// Gets the result type.
    /// </summary>
    WolframAlphaResultType Type { get; }

    /// <summary>
    /// Gets a value indicating whether the result is successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a read-only list containing alternative queries.
    /// </summary>
    IReadOnlyList<IWolframAlphaQuerySuggestion> DidYouMeans { get; }

    /// <summary>
    /// Gets the warnings.
    /// </summary>
    IReadOnlyList<IWolframAlphaWarning> Warnings { get; }

    /// <summary>
    /// Gets the error information.
    /// </summary>
    IWolframAlphaErrorInfo? ErrorInfo { get; }

    /// <summary>
    /// Gets the future topic.
    /// </summary>
    IWolframAlphaFutureTopic? FutureTopic { get; }

    /// <summary>
    /// Gets the pods.
    /// </summary>
    IReadOnlyList<IWolframAlphaPod> Pods { get; }
}