using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IEntryPronunciation"/>
public record EntryPronunciation([property: JsonPropertyName("ipa")] string Ipa) : IEntryPronunciation;