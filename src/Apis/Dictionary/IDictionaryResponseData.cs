namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the dictionary response data.
/// </summary>
public interface IDictionaryResponseData
{
    /// <summary>
    /// Gets an object containing the group of entries for the word.
    /// </summary>
    IDictionaryContent Content { get; }
}