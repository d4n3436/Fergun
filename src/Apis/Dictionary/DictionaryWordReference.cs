using JetBrains.Annotations;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryWordReference"/>
[UsedImplicitly]
public record DictionaryWordReference(
    [property: JsonPropertyName("identifier")] string Identifier,
    [property: JsonPropertyName("type")] string Type) : IDictionaryWordReference;