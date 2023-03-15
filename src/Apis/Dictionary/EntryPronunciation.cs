using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IEntryPronunciation"/>
public record EntryPronunciation(
    [property: JsonPropertyName("ipa")] string Ipa,
    [property: JsonConverter(typeof(ArrayOrStringConverter))]
    [property: JsonPropertyName("spell")] IReadOnlyList<string> Spell, // example with array response: bass
    [property: JsonPropertyName("audio")] EntryPronunciationAudio? Audio) : IEntryPronunciation
{
    /// <inheritdoc/>
    IEntryPronunciationAudio? IEntryPronunciation.Audio => Audio;
}