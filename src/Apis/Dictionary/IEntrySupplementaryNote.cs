using System.Collections.Generic;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the supplementary notes for an entry.
/// </summary>
public interface IEntrySupplementaryNote
{
    /// <summary>
    /// Gets the type of note.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Gets the content.
    /// </summary>
    IReadOnlyList<string> Content { get; }
}