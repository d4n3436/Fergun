using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryResponseData"/>
public record DictionaryResponseData(
    [property: JsonPropertyName("content")] IReadOnlyList<DictionaryEntryGroup> Content) : IDictionaryResponseData
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryEntryGroup> IDictionaryResponseData.Content => Content;
}