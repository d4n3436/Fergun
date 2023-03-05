using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IEntrySupplementaryNote"/>
public record EntrySupplementaryNote(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonConverter(typeof(ArrayOrStringConverter))]
    [property: JsonPropertyName("content")] IReadOnlyList<string> Content) : IEntrySupplementaryNote;