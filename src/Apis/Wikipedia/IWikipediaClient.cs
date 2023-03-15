using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a Wikipedia API client.
/// </summary>
public interface IWikipediaClient
{
    /// <summary>
    /// Gets a Wikipedia article by its ID.
    /// </summary>
    /// <param name="id">The ID of an article.</param>
    /// <param name="language">The search language.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The result contains the article.</returns>
    Task<IWikipediaArticle?> GetArticleAsync(int id, string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for Wikipedia articles.
    /// </summary>
    /// <param name="query">The search string.</param>
    /// <param name="language">The search language.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation. The result contains a read-only list of partial articles.</returns>
    Task<IReadOnlyList<IPartialWikipediaArticle>> SearchArticlesAsync(string query, string language,
        CancellationToken cancellationToken = default);
}