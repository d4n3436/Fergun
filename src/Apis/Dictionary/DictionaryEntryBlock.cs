using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntryBlock"/>
public record DictionaryEntryBlock(
    [property: JsonPropertyName("definitions")] IReadOnlyList<DictionaryDefinition> Definitions,
    [property: JsonPropertyName("pos")] string? PartOfSpeech,
    [property: JsonPropertyName("posSupplementaryInfo")] string SupplementaryInfo) : IDictionaryEntryBlock
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryDefinition> IDictionaryEntryBlock.Definitions => Definitions;
}