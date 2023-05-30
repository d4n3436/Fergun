using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a Bing Visual Search API.
/// </summary>
public interface IBingVisualSearch
{
    /// <summary>
    /// Performs reverse image search to the specified image URL.
    /// </summary>
    /// <param name="url">The URL of an image.</param>
    /// <param name="safeSearch">The safe search level.</param>
    /// <param name="language">The language of the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous search operation. The result contains an <see cref="IEnumerable{T}"/> of search results.</returns>
    Task<IEnumerable<IBingReverseImageSearchResult>> ReverseImageSearchAsync(string url,
        BingSafeSearchLevel safeSearch = BingSafeSearchLevel.Moderate, string? language = null,
        CancellationToken cancellationToken = default);
}