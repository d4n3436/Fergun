using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntry"/>
[UsedImplicitly]
public record DictionaryEntry([property: JsonPropertyName("entry")] string Entry,
    [property: JsonConverter(typeof(ArrayOrStringConverter))]
    [property: JsonPropertyName("entryVariants")] IReadOnlyList<string>? EntryVariants,
    [property: JsonPropertyName("homograph")] int? Homograph,
    [property: JsonConverter(typeof(PronunciationConverter))]
    [property: JsonPropertyName("pronunciation")] EntryPronunciation? Pronunciation,
    [property: JsonPropertyName("posBlocks")] IReadOnlyList<DictionaryEntryBlock> PartOfSpeechBlocks,
    [property: JsonPropertyName("origin")] string Origin) : IDictionaryEntry
{
    /// <inheritdoc/>
    IEntryPronunciation? IDictionaryEntry.Pronunciation => Pronunciation;

    /// <inheritdoc/>
    IReadOnlyList<IDictionaryEntryBlock> IDictionaryEntry.PartOfSpeechBlocks => PartOfSpeechBlocks; // empty in some cases like monks
}
