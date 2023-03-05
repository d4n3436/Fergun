namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a dictionary word.
/// </summary>
public interface IDictionaryWord
{
    /// <summary>
    /// Gets the display text.
    /// </summary>
    string DisplayText { get; }

    /// <summary>
    /// Gets the reference data.
    /// </summary>
    IDictionaryWordReference Reference { get; }
}