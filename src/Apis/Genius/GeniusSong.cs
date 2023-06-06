using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Genius;

/// <inheritdoc cref="IGeniusSong"/>
public record GeniusSong(
    [property: JsonPropertyName("artist_names")] string ArtistNames,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("instrumental")] bool IsInstrumental,
    [property: JsonPropertyName("lyrics_state")] string LyricsState,
    [property: JsonPropertyName("song_art_image_url")] string SongArtImageUrl,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("spotify_uuid")] string? SpotifyTrackId,
    [property: JsonConverter(typeof(LyricsConverter))]
    [property: JsonPropertyName("lyrics")] string? Lyrics,
    [property: JsonPropertyName("primary_artist")]
    [property: DebuggerBrowsable(DebuggerBrowsableState.Never)] GeniusPrimaryArtist PrimaryArtist) : IGeniusSong
{
    /// <inheritdoc/>
    [JsonIgnore]
    public string PrimaryArtistUrl => PrimaryArtist.Url;

    /// <summary>
    /// Returns the full title of this song.
    /// </summary>
    /// <returns>The full title of this song.</returns>
    public override string ToString() => $"{ArtistNames} - {Title}";
}