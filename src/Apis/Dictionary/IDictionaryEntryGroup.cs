using System.Collections.Generic;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a group of entries from a specific source.
/// </summary>
public interface IDictionaryEntryGroup
{
    /// <summary>
    /// Gets the source of the entries.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Gets a read-only list containing the entries.
    /// </summary>
    IReadOnlyList<IDictionaryEntry> Entries { get; }
}