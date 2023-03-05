using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryResponse"/>
public record DictionaryResponse(
    [property: JsonPropertyName("data")] DictionaryResponseData? Data) : IDictionaryResponse
{
    /// <inheritdoc/>
    IDictionaryResponseData? IDictionaryResponse.Data => Data;
}