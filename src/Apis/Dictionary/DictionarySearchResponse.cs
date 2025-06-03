using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionarySearchResponse"/>
[UsedImplicitly]
public record DictionarySearchResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<DictionaryWord> Data) : IDictionarySearchResponse
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryWord> IDictionarySearchResponse.Data => Data;
}