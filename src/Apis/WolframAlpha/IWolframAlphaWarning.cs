namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a WolframAlpha warning.
/// </summary>
public interface IWolframAlphaWarning
{
    /// <summary>
    /// Gets the description of this warning.
    /// </summary>
    string Text { get; }
}