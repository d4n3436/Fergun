using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.YouTube;

/// <summary>
/// Represents a minimal YouTube (InnerTube) search client.
/// </summary>
public interface IYouTubeClient
{
    /// <summary>
    /// Searches for videos matching the specified query and returns the first page of results.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result contains a read-only list of videos.</returns>
    Task<IReadOnlyList<YouTubeVideo>> SearchVideosAsync(string query, CancellationToken cancellationToken = default);
}