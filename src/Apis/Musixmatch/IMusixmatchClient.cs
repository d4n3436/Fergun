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
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains an <see cref="IEnumerable{T}"/> of matching songs.</returns>
    Task<IEnumerable<IMusixmatchSong>> SearchSongsAsync(string query, bool onlyWithLyrics = true);

    /// <summary>
    /// Gets a Musixmatch song by its ID.
    /// </summary>
    /// <param name="id">The ID of the song.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the song.</returns>
    Task<IMusixmatchSong?> GetSongAsync(int id);
}