namespace Fergun.Apis.Yandex;

/// <summary>
/// Specifies the filter modes in Yandex.Search.
/// </summary>
public enum YandexSearchFilterMode
{
    /// <summary>
    /// Search results include all the documents found for the query, including internet resources “for adults”.
    /// </summary>
    None = 0,

    /// <summary>
    /// Sites “for adults” are excluded from search results if the query does not explicitly search for such resources.
    /// </summary>
    Moderate = 1,

    /// <summary>
    /// Adult content and sites containing obscene language are completely excluded from search results (even if the query is clearly directed at finding such resources).
    /// </summary>
    Family = 2
}