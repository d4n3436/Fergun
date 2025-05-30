using System.Collections.Generic;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a part of speech block.
/// </summary>
public interface IDictionaryEntryBlock
{
    /// <summary>
    /// Gets a read-only list of definitions.
    /// </summary>
    IReadOnlyList<IDictionaryDefinition> Definitions { get; }

    /// <summary>
    /// Gets the part of speech.
    /// </summary>
    string? PartOfSpeech { get; }

    /// <summary>
    /// Gets the supplementary info.
    /// </summary>
    string SupplementaryInfo { get; }
}