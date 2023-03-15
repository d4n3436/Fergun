using System;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IEntryPronunciationAudio"/>
public record EntryPronunciationAudio(
    [property: JsonPropertyName("audio/ogg")] Uri Ogg,
    [property: JsonPropertyName("audio/mpeg")] Uri Mpeg) : IEntryPronunciationAudio;