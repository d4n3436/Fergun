namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents a Musixmatch song.
/// </summary>
public interface IMusixmatchSong
{
    /// <summary>
    /// Gets the artist name.
    /// </summary>
    string ArtistName { get; }

    /// <summary>
    /// Gets the ID of this song.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Gets a value indicating whether this song is instrumental.
    /// </summary>
    bool IsInstrumental { get; }

    /// <summary>
    /// Gets a value indicating whether this song has lyrics.
    /// </summary>
    bool HasLyrics { get; }

    /// <summary>
    /// Gets the song art image URL.
    /// </summary>
    string SongArtImageUrl { get; }

    /// <summary>
    /// Gets the title of this song.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets a URL pointing to the lyrics page.
    /// </summary>
    string Url { get; }

    /// <summary>
    /// Gets a URL pointing to the artist page.
    /// </summary>
    string? ArtistUrl { get; }

    /// <summary>
    /// Gets the lyrics of this song.
    /// </summary>
    string? Lyrics { get; }
}