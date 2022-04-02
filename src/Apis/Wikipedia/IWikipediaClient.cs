namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a Wikipedia API client.
/// </summary>
public interface IWikipediaClient
{
    /// <summary>
    /// Gets a collection of articles that matches <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The search string.</param>
    /// <param name="language">The search language.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation. The result contains an ordered <see cref="IEnumerable{T}"/> of articles.</returns>
    Task<IEnumerable<IWikipediaArticle>> GetArticlesAsync(string query, string language);

    /// <summary>
    /// Gets autocomplete results.
    /// </summary>
    /// <param name="query">The search string.</param>
    /// <param name="language">The search language.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation. The result contains a read-only list of autocomplete results.</returns>
    Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string query, string language);
}