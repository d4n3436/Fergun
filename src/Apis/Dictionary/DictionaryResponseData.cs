using JetBrains.Annotations;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryResponseData"/>
[UsedImplicitly]
public record DictionaryResponseData(
    [property: JsonPropertyName("content")] IReadOnlyList<DictionaryEntryGroup> Content) : IDictionaryResponseData // luna is not always present
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryEntryGroup> IDictionaryResponseData.Content => Content;
}