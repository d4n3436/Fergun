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
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains an <see cref="IEnumerable{T}"/> of matching songs.</returns>
    Task<IEnumerable<IGeniusSong>> SearchSongsAsync(string query);

    /// <summary>
    /// Gets a Genius song by its ID.
    /// </summary>
    /// <param name="id">The ID of the song.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the song.</returns>
    Task<IGeniusSong?> GetSongAsync(int id);
}