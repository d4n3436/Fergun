using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryResponseData"/>
[UsedImplicitly]
public record DictionaryResponseData(
    [property: JsonPropertyName("content")] DictionaryContent Content) : IDictionaryResponseData
{
    /// <inheritdoc/>
    IDictionaryContent IDictionaryResponseData.Content => Content;
}