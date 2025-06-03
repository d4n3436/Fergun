using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryWord"/>
[UsedImplicitly]
public record DictionaryWord(
    [property: JsonPropertyName("displayText")] string DisplayText,
    [property: JsonPropertyName("reference")] DictionaryWordReference Reference) : IDictionaryWord
{
    /// <inheritdoc/>
    IDictionaryWordReference IDictionaryWord.Reference => Reference;
}