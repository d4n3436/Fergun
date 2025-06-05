using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents the default Musixmatch client, using the internal API.
/// </summary>
public sealed class MusixmatchClient : IMusixmatchClient, IDisposable
{
    private const string AppId = "web-desktop-app-v1.0"; // community-app-v1.0, web-desktop-app-v1.0, android-player-v1.0, mac-ios-v2.0
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";
    private readonly AsyncRetryPolicy<JsonDocument> _retryPolicy;
    private readonly HttpClient _httpClient;
    private readonly MusixmatchClientState _state;
    private readonly ILogger<MusixmatchClient> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    /// <param name="state">The client state.</param>
    /// <param name="logger">The logger.</param>
    public MusixmatchClient(HttpClient httpClient, MusixmatchClientState state, ILogger<MusixmatchClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _state = state;
        _logger = logger;

        _retryPolicy = Policy<JsonDocument>
            .Handle<MusixmatchException>(x => x.Hint is "renew" or "captcha")
            .RetryAsync(async (result, _) =>
            {
                _logger.LogWarning(result.Exception, "Got exception with hint \"{Hint}\", requesting a new user token...", ((MusixmatchException)result.Exception).Hint);
                await _state.GetUserTokenAsync(refresh: true).ConfigureAwait(false);
            });

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IMusixmatchSong>> SearchSongsAsync(string query, bool onlyWithLyrics = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        string url = $"https://apic-desktop.musixmatch.com/ws/1.1/track.search?q_track_artist={Uri.EscapeDataString(query)}&s_track_rating=desc&format=json&app_id={AppId}&f_has_lyrics={(onlyWithLyrics ? 1 : 0)}&f_is_instrumental={(onlyWithLyrics ? 0 : 1)}";

        using var document = await _retryPolicy.ExecuteAsync(() => SendRequestAndValidateAsync(url, cancellationToken)).ConfigureAwait(false);

        return document
            .RootElement
            .GetProperty("message"u8)
            .GetProperty("body"u8)
            .GetProperty("track_list"u8)
            .EnumerateArray()
            .Select(x => x.GetProperty("track"u8).Deserialize<MusixmatchSong>()!)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IMusixmatchSong?> GetSongAsync(int id, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        string url = $"https://apic-desktop.musixmatch.com/ws/1.1/macro.community.lyrics.get?track_id={id}&version=2&format=json&app_id={AppId}";

        using var document = await _retryPolicy.ExecuteAsync(() => SendRequestAndValidateAsync(url, cancellationToken)).ConfigureAwait(false);

        var macroCalls = document
            .RootElement
            .GetProperty("message"u8)
            .GetProperty("body"u8)
            .GetProperty("macro_calls"u8)[0];

        var trackStatusCode = (HttpStatusCode)macroCalls
            .GetProperty("track.get"u8)
            .GetProperty("message"u8)
            .GetProperty("header"u8)
            .GetProperty("status_code"u8)
            .GetInt32();

        if (trackStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var lyricsStatusCode = (HttpStatusCode)macroCalls
            .GetProperty("track.lyrics.get"u8)
            .GetProperty("message"u8)
            .GetProperty("header"u8)
            .GetProperty("status_code"u8)
            .GetInt32();

        var lyricsData = lyricsStatusCode == HttpStatusCode.NotFound ? default : macroCalls
            .GetProperty("track.lyrics.get"u8)
            .GetProperty("message"u8)
            .GetProperty("body"u8)
            .GetProperty("lyrics"u8);

        string? lyrics = lyricsStatusCode == HttpStatusCode.NotFound ? null : lyricsData
            .GetProperty("lyrics_body"u8)
            .GetString();

        var trackData = macroCalls
            .GetProperty("track.get"u8)
            .GetProperty("message"u8)
            .GetProperty("body"u8)
            .GetProperty("track"u8);

        bool isRestricted = lyricsStatusCode == HttpStatusCode.NotFound ? trackData
            .GetProperty("restricted"u8)
            .GetInt32() != 0 : lyricsData
            .GetProperty("restricted"u8)
            .GetInt32() != 0;

        string artistName = trackData.GetProperty("artist_name"u8).GetString()!;
        bool isInstrumental = trackData.GetProperty("instrumental"u8).GetInt32() != 0;
        bool hasLyrics = trackData.GetProperty("has_lyrics"u8).GetInt32() != 0;
        string? songArtImageUrl = trackData.GetProperty("album_coverart_500x500"u8).GetString();
        string title = trackData.GetProperty("track_name"u8).GetString()!;
        int artistId = trackData.GetProperty("artist_id"u8).GetInt32();

        string? spotifyTrackId = null;
        if (trackData.TryGetProperty("track_spotify_id"u8, out var trackIdProp))
        {
            spotifyTrackId = trackIdProp.GetString();
        }

        string songUrl = $"https://www.musixmatch.com/track/{id}";
        string artistUrl = $"https://www.musixmatch.com/artist/{artistId}";
        if (string.IsNullOrEmpty(songArtImageUrl))
        {
            songArtImageUrl = "https://s.mxmcdn.net/images-storage/albums/nocover.png";
        }

        return new MusixmatchSong(artistName, id, isInstrumental, hasLyrics, isRestricted, songArtImageUrl, title, songUrl, artistUrl, lyrics, spotifyTrackId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    internal static void ThrowIfNotSuccessful(in JsonElement body, string? path = null)
    {
        var header = body
            .GetProperty("message"u8)
            .GetProperty("header"u8);

        var statusCode = (HttpStatusCode)header
            .GetProperty("status_code"u8)
            .GetInt32();

        if (statusCode is HttpStatusCode.OK or HttpStatusCode.NotFound) return;

        string? hint = header
            .GetProperty("hint"u8)
            .GetString();

        MusixmatchException.Throw(statusCode, path, hint);
    }

    private async Task<JsonDocument> SendRequestAndValidateAsync(string url, CancellationToken cancellationToken = default)
    {
        string userToken = await _state.GetUserTokenAsync().ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{url}&usertoken={userToken}"));
        request.Headers.Add("Cookie", "x-mxm-token-guid=undefined");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        ThrowIfNotSuccessful(document.RootElement);

        var body = document.RootElement.GetProperty("message"u8).GetProperty("body"u8);
        if (body.TryGetProperty("macro_calls"u8, out var macroCalls) && macroCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in macroCalls.EnumerateArray())
            {
                var first = prop.EnumerateObject().FirstOrDefault();
                if (first.Value.ValueKind == JsonValueKind.Object)
                {
                    ThrowIfNotSuccessful(first.Value, first.Name);
                }
            }
        }

        return document;
    }
}