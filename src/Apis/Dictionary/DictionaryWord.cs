using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryWord"/>
public record DictionaryWord(
    [property: JsonPropertyName("displayText")] string DisplayText,
    [property: JsonPropertyName("reference")] DictionaryWordReference Reference) : IDictionaryWord
{
    /// <inheritdoc/>
    IDictionaryWordReference IDictionaryWord.Reference => Reference;
}