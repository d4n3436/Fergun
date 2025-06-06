using System.Collections.Generic;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a dictionary entry.
/// </summary>
public interface IDictionaryEntry
{
    /// <summary>
    /// Gets the headword.
    /// </summary>
    string Entry { get; }

    /// <summary>
    /// Gets the variants of the entry.
    /// </summary>
    IReadOnlyList<string>? EntryVariants { get; }

    /// <summary>
    /// Gets the current homograph number.
    /// </summary>
    string? Homograph { get; }

    /// <summary>
    /// Gets the pronunciation data.
    /// </summary>
    IEntryPronunciation? Pronunciation { get; }

    /// <summary>
    /// Gets a read-only list containing blocks for every part of speech.
    /// </summary>
    IReadOnlyList<IDictionaryEntryBlock> PartOfSpeechBlocks { get; }

    /// <summary>
    /// Gets the origin of this entry.
    /// </summary>
    string Origin { get; }
}