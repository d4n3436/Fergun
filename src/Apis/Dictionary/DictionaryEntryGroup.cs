using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntryGroup"/>
[UsedImplicitly]
public record DictionaryEntryGroup(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("entries")] IReadOnlyList<DictionaryEntry> Entries) : IDictionaryEntryGroup
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryEntry> IDictionaryEntryGroup.Entries => Entries;
}