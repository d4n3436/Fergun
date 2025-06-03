using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryWordReference"/>
[UsedImplicitly]
public record DictionaryWordReference(
    [property: JsonPropertyName("identifier")] string Identifier,
    [property: JsonPropertyName("type")] string Type) : IDictionaryWordReference;