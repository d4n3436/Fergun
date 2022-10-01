namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Specifies the possible result types that WolframAlpha can return.
/// </summary>
public enum WolframAlphaResultType
{
    /// <summary>
    /// Unknown result.
    /// </summary>
    Unknown,

    /// <summary>
    /// Successful result.
    /// </summary>
    Success,

    /// <summary>
    /// Wolfram Alpha didn't understand the query and it provided alternatives.
    /// </summary>
    DidYouMean,

    /// <summary>
    /// The query result refers to a topic still under development.
    /// </summary>
    FutureTopic,

    /// <summary>
    /// No results.
    /// </summary>
    NoResult,

    /// <summary>
    /// An error occurred.
    /// </summary>
    Error
}