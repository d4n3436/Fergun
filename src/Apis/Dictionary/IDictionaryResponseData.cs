namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the dictionary response data.
/// </summary>
public interface IDictionaryResponseData
{
    /// <summary>
    /// Gets a read-only list containing a group of entries from a specific source.
    /// </summary>
    IReadOnlyList<IDictionaryEntryGroup> Content { get; }
}