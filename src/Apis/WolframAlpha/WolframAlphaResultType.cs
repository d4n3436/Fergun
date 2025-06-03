namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Specifies the possible result types that WolframAlpha can return.
/// </summary>
public enum WolframAlphaResultType
{
    /// <summary>
    /// No results.
    /// </summary>
    NoResult = 0,

    /// <summary>
    /// Successful result.
    /// </summary>
    Success = 1,

    /// <summary>
    /// Wolfram Alpha didn't understand the query and it provided alternatives.
    /// </summary>
    DidYouMean = 2,

    /// <summary>
    /// The query result refers to a topic still under development.
    /// </summary>
    FutureTopic = 3,

    /// <summary>
    /// An error occurred.
    /// </summary>
    Error = 4
}