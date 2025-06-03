using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IEntrySupplementaryNote"/>
[UsedImplicitly]
public record EntrySupplementaryNote(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonConverter(typeof(ArrayOrStringConverter))]
    [property: JsonPropertyName("content")] IReadOnlyList<string> Content) : IEntrySupplementaryNote;