using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntryBlock"/>
[UsedImplicitly]
public record DictionaryEntryBlock(
    [property: JsonPropertyName("definitions")] IReadOnlyList<DictionaryDefinition> Definitions,
    [property: JsonPropertyName("pos")] string? PartOfSpeech,
    [property: JsonPropertyName("posSupplementaryInfo")] string SupplementaryInfo) : IDictionaryEntryBlock
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryDefinition> IDictionaryEntryBlock.Definitions => Definitions;
}