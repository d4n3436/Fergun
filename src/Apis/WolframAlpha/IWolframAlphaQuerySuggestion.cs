namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a WolframAlpha query suggestion.
/// </summary>
public interface IWolframAlphaQuerySuggestion
{
    /// <summary>
    /// Gets the score. 
    /// </summary>
    float Score { get; }

    /// <summary>
    /// Gets the level.
    /// </summary>
    string Level { get; }

    /// <summary>
    /// Gets the suggestion.
    /// </summary>
    string Value { get; }
}