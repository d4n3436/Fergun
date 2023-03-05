namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the reference data for a dictionary word.
/// </summary>
public interface IDictionaryWordReference
{
    /// <summary>
    /// Gets the word identifier.
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Gets the type of result.
    /// </summary>
    string Type { get; }
}