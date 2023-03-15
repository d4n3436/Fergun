using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionarySearchResponse"/>
public record DictionarySearchResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<DictionaryWord> Data) : IDictionarySearchResponse
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryWord> IDictionarySearchResponse.Data => Data;
}