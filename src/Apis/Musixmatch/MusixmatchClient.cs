using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Fergun.Apis.Musixmatch;

/// <summary>
/// Represents the default Musixmatch client, using the internal API.
/// </summary>
public sealed class MusixmatchClient : IMusixmatchClient, IDisposable
{
    // a73051eb424674a19bc323eb5c5b6629fbc8ef46, treated literally
    private static ReadOnlySpan<byte> SignatureSecret => new byte[]
    {
        97, 55, 51, 48, 53, 49, 101, 98, 52, 50, 52, 54, 55, 52, 97, 49, 57, 98, 99, 51, 50, 51, 101, 98, 53, 99, 53, 98, 54, 54, 50, 57, 102, 98, 99, 56, 101, 102, 52, 54
    };

    private const string _appId = "community-app-v1.0";
    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchClient"/> class.
    /// </summary>
    public MusixmatchClient()
        : this(new HttpClient(new HttpClientHandler { UseCookies = false }))
    {
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="MusixmatchClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public MusixmatchClient(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <inheritdoc cref="IMusixmatchClient.SearchSongsAsync(string, bool)"/>
    public async Task<IEnumerable<MusixmatchSong>> SearchSongsAsync(string query, bool onlyWithLyrics = true)
    {
        EnsureNotDisposed();

        string url = $"https://www.musixmatch.com/ws/1.1/track.search?q_track_artist={Uri.EscapeDataString(query)}&s_track_rating=desc&format=json&app_id={_appId}&f_has_lyrics={(onlyWithLyrics ? 1 : 0)}&f_is_instrumental={(onlyWithLyrics ? 0 : 1)}";
        url = $"{url}&signature={GetSignature(url)}&signature_protocol=sha1";

        var stream = await _httpClient.GetStreamAsync(new Uri(url)).ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var message = document.RootElement.GetProperty("message");

        var statusCode = (HttpStatusCode)message
            .GetProperty("header")
            .GetProperty("status_code")
            .GetInt32();

        if (statusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException($"The API returned a {statusCode} status code.", null, statusCode);
        }

        return message
            .GetProperty("body")
            .GetProperty("track_list")
            .EnumerateArray()
            .Select(x => x.GetProperty("track").Deserialize<MusixmatchSong>()!);
    }

    /// <inheritdoc cref="IMusixmatchClient.GetSongAsync(int)"/>
    public async Task<MusixmatchSong?> GetSongAsync(int id)
    {
        EnsureNotDisposed();

        string url = $"https://www.musixmatch.com/ws/1.1/macro.community.lyrics.get?track_id={id}&version=2&format=json&app_id={_appId}";
        url = $"{url}&signature={GetSignature(url)}&signature_protocol=sha1";

        await using var stream = await _httpClient.GetStreamAsync(new Uri(url)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var message = document.RootElement.GetProperty("message");

        var statusCode = (HttpStatusCode)message
            .GetProperty("header")
            .GetProperty("status_code")
            .GetInt32();

        if (statusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException($"The API returned a {(int)statusCode} ({statusCode}) status code.", null, statusCode);
        }

        var macroCalls = message
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

            throw new HttpRequestException($"The track.get API method returned a {(int)trackStatusCode} ({trackStatusCode}) status code.", null, trackStatusCode);
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

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MusixmatchClient));
        }
    }

    private static string GetSignature(string source)
    {
        // Get SHA1 HMAC as Base64
        var date = DateTimeOffset.UtcNow;
        byte[] bytes = HMACSHA1.HashData(SignatureSecret, Encoding.UTF8.GetBytes($"{source}{date.Year}{date.Month:D2}{date.Day:D2}"));
        return Uri.EscapeDataString(Convert.ToBase64String(bytes));
    }

    /// <inheritdoc/>
    async Task<IEnumerable<IMusixmatchSong>> IMusixmatchClient.SearchSongsAsync(string query, bool onlyWithLyrics) => await SearchSongsAsync(query, onlyWithLyrics).ConfigureAwait(false);

    /// <inheritdoc/>
    async Task<IMusixmatchSong?> IMusixmatchClient.GetSongAsync(int id) => await GetSongAsync(id).ConfigureAwait(false);
}