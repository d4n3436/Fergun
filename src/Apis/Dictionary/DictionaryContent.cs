using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryResponseData"/>
[UsedImplicitly]
public record DictionaryContent(
    [property: JsonPropertyName("luna")] DictionaryEntryGroup? Luna,
    [property: JsonPropertyName("collins")] DictionaryEntryGroup? Collins) : IDictionaryContent
{
    /// <inheritdoc/>
    IDictionaryEntryGroup? IDictionaryContent.Luna => Luna;

    /// <inheritdoc/>
    IDictionaryEntryGroup? IDictionaryContent.Collins => Collins;
}