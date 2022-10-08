namespace Fergun.Apis.Genius;

/// <summary>
/// Represents a Genius API client.
/// </summary>
public interface IGeniusClient
{
    /// <summary>
    /// Searches for Genius songs that matches <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The search term.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only list of matching songs.</returns>
    Task<IReadOnlyList<IGeniusSong>> SearchSongsAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a Genius song by its ID.
    /// </summary>
    /// <param name="id">The ID of the song.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the song.</returns>
    Task<IGeniusSong?> GetSongAsync(int id, CancellationToken cancellationToken = default);
}