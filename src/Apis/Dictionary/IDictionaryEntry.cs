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
    IReadOnlyList<string> EntryVariants { get; }

    /// <summary>
    /// Gets the current homograph number.
    /// </summary>
    int? Homograph { get; }

    /// <summary>
    /// Gets the pronunctiation data.
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

    /// <summary>
    /// Gets the supplementary notes.
    /// </summary>
    IReadOnlyList<IEntrySupplementaryNote> SupplementaryNotes { get; }

    /// <summary>
    /// Gets the variant spellings.
    /// </summary>
    IReadOnlyList<string> VariantSpellings { get; }
}