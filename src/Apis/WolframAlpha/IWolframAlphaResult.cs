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
    /// Gets a read-only list containing alternative queries.
    /// </summary>
    IReadOnlyList<string> DidYouMean { get; }

    /// <summary>
    /// Gets the future topic.
    /// </summary>
    IWolframAlphaFutureTopic? FutureTopic { get; }

    /// <summary>
    /// Gets the error status code.
    /// </summary>
    int? StatusCode { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the pods.
    /// </summary>
    IReadOnlyList<IWolframAlphaPod> Pods { get; }
}