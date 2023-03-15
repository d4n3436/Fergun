using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents a Musixmatch client.
/// </summary>
public interface IMusixmatchClient
{
    /// <summary>
    /// Searches for Musixmatch songs that matches <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The search term.</param>
    /// <param name="onlyWithLyrics">Whether to only search songs with lyrics.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation. The result contains a read-only list of matching songs.</returns>
    Task<IReadOnlyList<IMusixmatchSong>> SearchSongsAsync(string query, bool onlyWithLyrics = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a Musixmatch song by its ID.
    /// </summary>
    /// <param name="id">The ID of the song.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the song.</returns>
    Task<IMusixmatchSong?> GetSongAsync(int id, CancellationToken cancellationToken = default);
}