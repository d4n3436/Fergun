using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntry"/>
public record DictionaryEntry([property: JsonPropertyName("entry")] string Entry,
    [property: JsonConverter(typeof(ArrayOrStringConverter))]
    [property: JsonPropertyName("entryVariants")] IReadOnlyList<string> EntryVariants,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [property: JsonPropertyName("homograph")] int? Homograph,
    [property: JsonPropertyName("pronunciation")] EntryPronunciation? Pronunciation,
    [property: JsonPropertyName("posBlocks")] IReadOnlyList<DictionaryEntryBlock> PartOfSpeechBlocks,
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("supplementaryNotes")] IReadOnlyList<EntrySupplementaryNote>? SupplementaryNotes,
    [property: JsonPropertyName("variantSpellings")] IReadOnlyList<string>? VariantSpellings) : IDictionaryEntry
{
    /// <inheritdoc/>
    IEntryPronunciation? IDictionaryEntry.Pronunciation => Pronunciation;

    /// <inheritdoc/>
    IReadOnlyList<IDictionaryEntryBlock> IDictionaryEntry.PartOfSpeechBlocks => PartOfSpeechBlocks; // empty in some cases like monks

    /// <inheritdoc/>
    IReadOnlyList<IEntrySupplementaryNote>? IDictionaryEntry.SupplementaryNotes => SupplementaryNotes;
}
