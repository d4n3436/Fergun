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

    /// <inheritdoc cref="IMusixmatchClient.SearchSongsAsync(string, bool)"/>
    public async Task<IEnumerable<MusixmatchSong>> SearchSongsAsync(string query, bool onlyWithLyrics = true)
    {
        EnsureNotDisposed();

        string url = $"https://apic-desktop.musixmatch.com/ws/1.1/track.search?q_track_artist={Uri.EscapeDataString(query)}&s_track_rating=desc&format=json&app_id={_appId}&f_has_lyrics={(onlyWithLyrics ? 1 : 0)}&f_is_instrumental={(onlyWithLyrics ? 0 : 1)}";

        var document = await _retryPolicy.ExecuteAsync(() => SendRequestAndValidateAsync(url)).ConfigureAwait(false);

        return document
            .RootElement
            .GetProperty("message")
            .GetProperty("body")
            .GetProperty("track_list")
            .EnumerateArray()
            .Select(x => x.GetProperty("track").Deserialize<MusixmatchSong>()!);
    }

    /// <inheritdoc cref="IMusixmatchClient.GetSongAsync(int)"/>
    public async Task<MusixmatchSong?> GetSongAsync(int id)
    {
        EnsureNotDisposed();

        string url = $"https://apic-desktop.musixmatch.com/ws/1.1/macro.community.lyrics.get?track_id={id}&version=2&format=json&app_id={_appId}";

        using var document = await _retryPolicy.ExecuteAsync(() => SendRequestAndValidateAsync(url)).ConfigureAwait(false);

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

        var lyricsMessage = macroCalls
            .GetProperty("track.lyrics.get")
            .GetProperty("message");

        string? lyrics = lyricsMessage
            .GetProperty("header")
            .GetProperty("status_code")
            .GetInt32() == 404 ? null
            : lyricsMessage
            .GetProperty("body")
            .GetProperty("lyrics")
            .GetProperty("lyrics_body")
            .GetString();

        bool isRestricted = lyricsMessage
            .GetProperty("body")
            .GetProperty("lyrics")
            .GetProperty("restricted")
            .GetInt32() != 0; // from lyricsMessage

        var trackData = macroCalls
            .GetProperty("track.get")
            .GetProperty("message")
            .GetProperty("body")
            .GetProperty("track");

        string artistName = trackData.GetProperty("artist_name").GetString() ?? throw new MusixmatchException("Unable to get the artist name.");
        bool isInstrumental = trackData.GetProperty("instrumental").GetInt32() != 0;
        bool hasLyrics = trackData.GetProperty("has_lyrics").GetInt32() != 0;
        string? songArtImageUrl = trackData.GetProperty("album_coverart_500x500").GetString();
        string title = trackData.GetProperty("track_name").GetString() ?? throw new MusixmatchException("Unable to get the song name.");
        int artistId = trackData.GetProperty("artist_id").GetInt32();

        string songUrl = $"https://www.musixmatch.com/track/{id}";
        string artistUrl = $"https://www.musixmatch.com/artist/{artistId}";
        if (string.IsNullOrEmpty(songArtImageUrl))
        {
            songArtImageUrl = "https://s.mxmcdn.net/images-storage/albums/nocover.png";
        }

        return new MusixmatchSong(artistName, id, isInstrumental, hasLyrics, isRestricted, songArtImageUrl, title, songUrl, artistUrl, lyrics);
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

    private async Task<JsonDocument> SendRequestAndValidateAsync(string url)
    {
        string userToken = await _state.GetUserTokenAsync().ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{url}&usertoken={userToken}"));
        request.Headers.Add("Cookie", "x-mxm-token-guid=undefined");

        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

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

    /// <inheritdoc/>
    async Task<IEnumerable<IMusixmatchSong>> IMusixmatchClient.SearchSongsAsync(string query, bool onlyWithLyrics) => await SearchSongsAsync(query, onlyWithLyrics).ConfigureAwait(false);

    /// <inheritdoc/>
    async Task<IMusixmatchSong?> IMusixmatchClient.GetSongAsync(int id) => await GetSongAsync(id).ConfigureAwait(false);
}