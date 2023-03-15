using System.Collections.Generic;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a dictionary word search response.
/// </summary>
public interface IDictionarySearchResponse
{
    /// <summary>
    /// Gets a read-only list containing the search results.
    /// </summary>
    IReadOnlyList<IDictionaryWord> Data { get; }
}