namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the dictionary response.
/// </summary>
public interface IDictionaryResponse
{
    /// <summary>
    /// Gets the response data.
    /// </summary>
    IDictionaryResponseData? Data { get; }
}