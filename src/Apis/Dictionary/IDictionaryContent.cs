namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the content of a dictionary response.
/// </summary>
public interface IDictionaryContent
{
    /// <summary>
    /// Gets the Luna (american definitions) entry group.
    /// </summary>
    IDictionaryEntryGroup? Luna { get; }

    /// <summary>
    /// Gets the Collins (british definitions) entry group.
    /// </summary>
    IDictionaryEntryGroup? Collins { get; }
}