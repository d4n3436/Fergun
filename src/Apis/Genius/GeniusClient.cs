using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Genius;

/// <summary>
/// Represents an API wrapper for Genius.
/// </summary>
public sealed class GeniusClient : IGeniusClient, IDisposable
{
    private const string GENIUS_LOGGED_OUT_TOKEN = "ZTejoT_ojOEasIkT9WrMBhBQOz6eYKK5QULCMECmOhvwqjRZ6WbpamFe3geHnvp3"; // Hardcoded token from Android app
    private const string VERSION_NAME = "5.16.0";

    private const string BOLD = "b";
    private const string DFP_UNIT = "dfp-unit";
    private const string HORIZONTAL_LINE = "hr";
    private const string IMAGE = "img";
    private const string ITALIC = "i";
    private const string LINE_BREAK = "br";
    private const string LINK = "a";
    private const string UNDERLINE = "u";

    private const string DefaultUserAgent = $"Genius/{VERSION_NAME} (Android)";
    private static readonly Uri _apiEndpoint = new("https://api.genius.com/");

    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeniusClient"/> class.
    /// </summary>
    public GeniusClient()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeniusClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public GeniusClient(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress ??= _apiEndpoint;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Genius-Android-Version", VERSION_NAME);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GENIUS_LOGGED_OUT_TOKEN);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Genius-Logged-Out", "true");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IGeniusSong>> SearchSongsAsync(string query, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var response = await _httpClient.GetAsync(new Uri($"search?q={Uri.EscapeDataString(query)}", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        return document.RootElement
            .GetProperty("response")
            .GetProperty("hits")
            .EnumerateArray()
            .Select(x => x.GetProperty("result").Deserialize<GeniusSong>()!)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IGeniusSong?> GetSongAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var response = await _httpClient.GetAsync(new Uri($"songs/{id}", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        var song = document
            .RootElement
            .GetProperty("response")
            .GetProperty("song");

        string artistNames = song.GetProperty("artist_names").GetString() ?? throw new GeniusException("Unable to get the artist names.");
        string headerImageUrl = song.GetProperty("header_image_url").GetString() ?? throw new GeniusException("Unable to get the song header image URL.");
        bool isInstrumental = song.GetProperty("instrumental").GetBoolean();
        string lyricsState = song.GetProperty("lyrics_state").GetString() ?? throw new GeniusException("Unable to get the lyrics state.");
        string songArtImageUrl = song.GetProperty("song_art_image_url").GetString() ?? throw new GeniusException("Unable to get the song art image URL.");
        string title = song.GetProperty("title").GetString() ?? throw new GeniusException("Unable to get the song title.");
        string url = song.GetProperty("url").GetString() ?? throw new GeniusException("Unable to get the lyrics page URL.");
        string? spotifyTrackId = song.GetProperty("spotify_uuid").GetString();

        string primaryArtistUrl = song
            .GetProperty("primary_artist")
            .GetProperty("url")
            .GetString() ?? throw new GeniusException("Unable to get the primary artist page URL.");

        var content = song.GetProperty("lyrics")
            .GetProperty("dom");

        var lyricsBuilder = new StringBuilder();
        IterateContent(content, lyricsBuilder);

        return new GeniusSong(artistNames, headerImageUrl, id, isInstrumental, lyricsState,
            songArtImageUrl, title, url, primaryArtistUrl, spotifyTrackId, lyricsBuilder.ToString());
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

    private static void IterateContent(in JsonElement element, StringBuilder builder)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            builder.Append(element.GetString());
        }
        else if (element.ValueKind == JsonValueKind.Object) // either tag or tag + children
        {
            var tag = element.GetProperty("tag");
            (string markDownStart, string markDownEnd) = tag.GetString() switch
            {
                ITALIC => ("*", "*"),
                BOLD => ("**", "**"),
                LINE_BREAK or HORIZONTAL_LINE or IMAGE or DFP_UNIT => ("\n", string.Empty),
                UNDERLINE => ("__", "__"),
                "h1" => ("\n# ", "\n"),
                "h2" => ("\n## ", "\n"),
                "h3" => ("\n### ", "\n"),
                _ => (string.Empty, string.Empty)
            };

            if (builder.Length > 0 && builder[^1] is '*' or '_')
            {
                builder.Append('\u200b'); // Append zero-witdh space to prevent markdown from breaking
            }
            builder.Append(markDownStart);

            if (element.TryGetProperty("children", out var children))
            {
                Debug.Assert(children.ValueKind == JsonValueKind.Array);

                foreach (var child in children.EnumerateArray())
                {
                    IterateContent(child, builder);
                }

                builder.Append(markDownEnd);
            }
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GeniusClient));
        }
    }
}