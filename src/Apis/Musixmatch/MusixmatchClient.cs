using System.Net;
using System.Text.Json;
using Polly;
using Polly.Retry;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents the default Musixmatch client, using the internal API.
/// </summary>
public sealed class MusixmatchClient : IMusixmatchClient, IDisposable
{
    private const string _appId = "web-desktop-app-v1.0"; // community-app-v1.0, web-desktop-app-v1.0
    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";
    private readonly AsyncRetryPolicy<JsonDocument> _retryPolicy;
    private readonly HttpClient _httpClient;
    private readonly MusixmatchClientState _state;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    /// <param name="state">The client state.</param>
    public MusixmatchClient(HttpClient httpClient, MusixmatchClientState state)
    {
        _httpClient = httpClient;
        _state = state;
        _retryPolicy = Policy<JsonDocument>
            .Handle<MusixmatchException>(x => x.Hint == "renew")
            .RetryAsync(async (_, _) => await _state.GetUserTokenAsync(refresh: true).ConfigureAwait(false));

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IMusixmatchSong>> SearchSongsAsync(string query, bool onlyWithLyrics = true, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string url = $"https://apic-desktop.musixmatch.com/ws/1.1/track.search?q_track_artist={Uri.EscapeDataString(query)}&s_track_rating=desc&format=json&app_id={_appId}&f_has_lyrics={(onlyWithLyrics ? 1 : 0)}&f_is_instrumental={(onlyWithLyrics ? 0 : 1)}";

        using var document = await _retryPolicy.ExecuteAsync(() => SendRequestAndValidateAsync(url, cancellationToken)).ConfigureAwait(false);

        return document
            .RootElement
            .GetProperty("message")
            .GetProperty("body")
            .GetProperty("track_list")
            .EnumerateArray()
            .Select(x => x.GetProperty("track").Deserialize<MusixmatchSong>()!)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IMusixmatchSong?> GetSongAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string url = $"https://apic-desktop.musixmatch.com/ws/1.1/macro.community.lyrics.get?track_id={id}&version=2&format=json&app_id={_appId}";

        using var document = await _retryPolicy.ExecuteAsync(() => SendRequestAndValidateAsync(url, cancellationToken)).ConfigureAwait(false);

        var macroCalls = document
            .RootElement
            .GetProperty("message")
            .GetProperty("body")
            .GetProperty("macro_calls")[0];

        var trackStatusCode = (HttpStatusCode)macroCalls
            .GetProperty("track.get")
            .GetProperty("message")
            .GetProperty("header")
            .GetProperty("status_code")
            .GetInt32();

        if (trackStatusCode != HttpStatusCode.OK)
        {
            if (trackStatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            MusixmatchException.Throw(trackStatusCode, null);
        }

        var trackData = macroCalls
            .GetProperty("track.get")
            .GetProperty("message")
            .GetProperty("body")
            .GetProperty("track");

        var lyricsMessage = macroCalls
            .GetProperty("track.lyrics.get")
            .GetProperty("message");

        int lyricsStatusCode = lyricsMessage
            .GetProperty("header")
            .GetProperty("status_code")
            .GetInt32();

        string? lyrics = lyricsStatusCode == 404 ? null : lyricsMessage
            .GetProperty("body")
            .GetProperty("lyrics")
            .GetProperty("lyrics_body")
            .GetString();

        bool isRestricted = lyricsStatusCode == 404 ? trackData
            .GetProperty("restricted")
            .GetInt32() != 0 : lyricsMessage
            .GetProperty("body")
            .GetProperty("lyrics")
            .GetProperty("restricted")
            .GetInt32() != 0;

        string artistName = trackData.GetProperty("artist_name").GetString() ?? throw new MusixmatchException("Unable to get the artist name.");
        bool isInstrumental = trackData.GetProperty("instrumental").GetInt32() != 0;
        bool hasLyrics = trackData.GetProperty("has_lyrics").GetInt32() != 0;
        string? songArtImageUrl = trackData.GetProperty("album_coverart_500x500").GetString();
        string title = trackData.GetProperty("track_name").GetString() ?? throw new MusixmatchException("Unable to get the song name.");
        int artistId = trackData.GetProperty("artist_id").GetInt32();

        string? spotifyTrackId = null;
        if (trackData.TryGetProperty("track_spotify_id", out var trackIdProp))
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

    private async Task<JsonDocument> SendRequestAndValidateAsync(string url, CancellationToken cancellationToken = default)
    {
        string userToken = await _state.GetUserTokenAsync().ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{url}&usertoken={userToken}"));
        request.Headers.Add("Cookie", "x-mxm-token-guid=undefined");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        var statusCode = (HttpStatusCode)document
            .RootElement
            .GetProperty("message")
            .GetProperty("header")
            .GetProperty("status_code")
            .GetInt32();

        if (statusCode != HttpStatusCode.OK)
        {
            string? hint = document
                .RootElement
                .GetProperty("message")
                .GetProperty("header")
                .GetProperty("hint")
                .GetString();

            MusixmatchException.Throw(statusCode, hint);
        }

        return document;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MusixmatchClient));
        }
    }
}