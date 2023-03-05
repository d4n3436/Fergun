using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntryGroup"/>
public record DictionaryEntryGroup(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("entries")] IReadOnlyList<DictionaryEntry> Entries) : IDictionaryEntryGroup
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryEntry> IDictionaryEntryGroup.Entries => Entries;
}