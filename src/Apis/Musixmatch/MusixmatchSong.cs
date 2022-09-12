using System.Text.Json.Serialization;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents a Musixmatch song (track).
/// </summary>
public class MusixmatchSong : IMusixmatchSong
{
    public MusixmatchSong(string artistName, int id, bool isInstrumental, bool hasLyrics, bool isRestricted,
        string songArtImageUrl, string title, string url, string? artistUrl, string? lyrics)
    {
        ArtistName = artistName;
        Id = id;
        IsInstrumental = isInstrumental;
        HasLyrics = hasLyrics;
        IsRestricted = isRestricted;
        SongArtImageUrl = songArtImageUrl;
        Title = title;
        Url = url;
        ArtistUrl = artistUrl;
        Lyrics = lyrics;
    }

    /// <inheritdoc/>
    [JsonPropertyName("artist_name")]
    public string ArtistName { get; }

    /// <inheritdoc/>
    [JsonPropertyName("track_id")]
    public int Id { get; }

    /// <inheritdoc/>
    [JsonPropertyName("instrumental")]
    [JsonConverter(typeof(BoolConverter))]
    public bool IsInstrumental { get; }

    /// <inheritdoc/>
    [JsonPropertyName("has_lyrics")]
    [JsonConverter(typeof(BoolConverter))]
    public bool HasLyrics { get; }

    [JsonPropertyName("restricted")]
    [JsonConverter(typeof(BoolConverter))]
    public bool IsRestricted { get; }

    /// <inheritdoc/>
    [JsonPropertyName("album_coverart_500x500")]
    public string SongArtImageUrl { get; }

    /// <inheritdoc/>
    [JsonPropertyName("track_name")]
    public string Title { get; }

    /// <inheritdoc/>
    [JsonPropertyName("track_share_url")]
    public string Url { get; }

    /// <inheritdoc/>
    public string? ArtistUrl { get; }

    /// <inheritdoc/>
    public string? Lyrics { get; }

    /// <summary>
    /// Returns the full title of this song.
    /// </summary>
    /// <returns>The full title of this song.</returns>
    public override string ToString() => $"{ArtistName} - {Title}";
}