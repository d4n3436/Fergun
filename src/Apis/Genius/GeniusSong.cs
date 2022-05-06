﻿using System.Drawing;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Genius;

/// <summary>
/// Represents a Genius song.
/// </summary>
public class GeniusSong : IGeniusSong
{
    public GeniusSong(string artistNames, string headerImageUrl, int id, bool isInstrumental,
        string songArtImageUrl, string title, string url, string? primaryArtistUrl, Color? primaryArtColor, string? lyrics)
    {
        ArtistNames = artistNames;
        HeaderImageUrl = headerImageUrl;
        Id = id;
        IsInstrumental = isInstrumental;
        SongArtImageUrl = songArtImageUrl;
        Title = title;
        Url = url;
        Id = id;
        PrimaryArtistUrl = primaryArtistUrl;
        PrimaryArtColor = primaryArtColor;
        Lyrics = lyrics;
    }

    /// <inheritdoc/>
    [JsonPropertyName("artist_names")]
    public string ArtistNames { get; }

    /// <inheritdoc/>
    [JsonPropertyName("header_image_url")]
    public string HeaderImageUrl { get; }

    /// <inheritdoc/>
    [JsonPropertyName("id")]
    public int Id { get; }

    /// <inheritdoc/>
    [JsonPropertyName("instrumental")]
    public bool IsInstrumental { get; }

    /// <inheritdoc/>
    [JsonPropertyName("song_art_image_url")]
    public string SongArtImageUrl { get; }

    /// <inheritdoc/>
    [JsonPropertyName("title")]
    public string Title { get; }

    /// <inheritdoc/>
    [JsonPropertyName("url")]
    public string Url { get; }

    /// <inheritdoc/>
    public string? PrimaryArtistUrl { get; }

    /// <inheritdoc/>
    public Color? PrimaryArtColor { get; }

    /// <inheritdoc/>
    public string? Lyrics { get; }

    /// <summary>
    /// Returns the full title of this song.
    /// </summary>
    /// <returns>The full title of this song.</returns>
    public override string ToString() => $"{ArtistNames} - {Title}";
}