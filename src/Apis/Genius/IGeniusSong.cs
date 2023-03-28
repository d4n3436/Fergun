using System.Drawing;

namespace Fergun.Apis.Genius;

/// <summary>
/// Represents a Genius song.
/// </summary>
public interface IGeniusSong
{
    /// <summary>
    /// Gets the artist names.
    /// </summary>
    string ArtistNames { get; }

    /// <summary>
    /// Gets the header image URL.
    /// </summary>
    string HeaderImageUrl { get; }

    /// <summary>
    /// Gets the ID of this song.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Gets a value indicating whether this song is instrumental.
    /// </summary>
    bool IsInstrumental { get; }

    /// <summary>
    /// Gets the state of the lyrics. It can be one of the following:<br/>
    /// - <c>instrumental</c><br/>
    /// - <c>unreleased</c><br/>
    /// - <c>incomplete</c><br/>
    /// - <c>complete</c>
    /// </summary>
    string LyricsState { get; }

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
    /// Gets a URL pointing to the primary artist page.
    /// </summary>
    public string? PrimaryArtistUrl { get; }

    /// <summary>
    /// Gets the primary art color.
    /// </summary>
    public Color? PrimaryArtColor { get; }

    /// <summary>
    /// Gets the lyrics of this song.
    /// </summary>
    string? Lyrics { get; }
}