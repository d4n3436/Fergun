namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Contains information about an error in WolframAlpha.
/// </summary>
public interface IWolframAlphaErrorInfo
{
    /// <summary>
    /// Gets the error status code.
    /// </summary>
    int StatusCode { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    string Message { get; }
}