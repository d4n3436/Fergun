using System.Drawing;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fergun.Apis.Genius;

/// <summary>
/// Represents an API wrapper for Genius.
/// </summary>
public sealed class GeniusClient : IGeniusClient, IDisposable
{
    private static ReadOnlySpan<byte> StartString => Encoding.UTF8.GetBytes("window.__PRELOADED_STATE__ = JSON.parse('");
    private static ReadOnlySpan<byte> EndString => new[] { (byte)'\'', (byte)')', (byte)';', (byte)'\n' };

    private static readonly Uri _apiEndpoint = new("https://genius.com/");

    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";
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
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <inheritdoc cref="IGeniusClient.SearchSongsAsync(string)"/>
    public async Task<IEnumerable<GeniusSong>> SearchSongsAsync(string query)
    {
        EnsureNotDisposed();

        var response = await _httpClient.GetAsync(new Uri($"api/search?q={Uri.EscapeDataString(query)}", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        return document.RootElement
            .GetProperty("response")
            .GetProperty("hits")
            .EnumerateArray()
            .Select(x => x.GetProperty("result").Deserialize<GeniusSong>()!);
    }

    /// <inheritdoc cref="IGeniusClient.GetSongAsync(int)"/>
    public async Task<GeniusSong?> GetSongAsync(int id)
    {
        EnsureNotDisposed();

        // The API doesn't provide the lyrics, so we scrape the lyrics page and extract the embedded JSON which contains the lyrics.
        using var response = await _httpClient.GetAsync(new Uri($"songs/{id}", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        int start = bytes.AsSpan().IndexOf(StartString);
        if (start == -1)
        {
            throw new GeniusException("Failed the extract the embedded JSON from the lyrics page.");
        }

        start += StartString.Length;

        int length = bytes.AsSpan(start).IndexOf(EndString);
        if (length == -1)
        {
            throw new GeniusException("Failed the extract the embedded JSON from the lyrics page.");
        }

        string data = Encoding.UTF8.GetString(bytes.AsSpan(start, length));

        var document = JsonDocument.Parse(Regex.Unescape(data));

        var song = document
            .RootElement
            .GetProperty("entities")
            .GetProperty("songs")
            .GetProperty(id.ToString());

        string artistNames = song.GetProperty("artistNames").GetString() ?? throw new GeniusException("Unable to get the artist names.");
        string headerImageUrl = song.GetProperty("headerImageUrl").GetString() ?? throw new GeniusException("Unable to get the song header image URL.");
        bool isInstrumental = song.GetProperty("instrumental").GetBoolean();
        string lyricsState = song.GetProperty("lyricsState").GetString() ?? throw new GeniusException("Unable to get the lyrics state.");
        string songArtImageUrl = song.GetProperty("songArtImageUrl").GetString() ?? throw new GeniusException("Unable to get the song art image URL.");
        string title = song.GetProperty("title").GetString() ?? throw new GeniusException("Unable to get the song title.");
        string url = song.GetProperty("url").GetString() ?? throw new GeniusException("Unable to get the lyrics page URL.");
        string songArtPrimaryColor = song.GetProperty("songArtPrimaryColor").GetString() ?? throw new GeniusException("Unable to get the primary art color.");
        Color? primaryColor = int.TryParse(songArtPrimaryColor.AsSpan().TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int color)
            ? Color.FromArgb(color)
            : default;

        int primaryArtistId = song.GetProperty("primaryArtist").GetInt32();
        string? primaryArtistUrl = document
            .RootElement
            .GetProperty("entities")
            .GetProperty("artists")
            .GetProperty(primaryArtistId.ToString())
            .GetProperty("url")
            .GetString();

        document.RootElement
            .GetProperty("songPage")
            .GetProperty("lyricsData")
            .GetProperty("body")
            .TryGetProperty("children", out var chunks);

        StringBuilder? lyricsBuilder = null;

        if (chunks.ValueKind != JsonValueKind.Undefined)
        {
            lyricsBuilder = new StringBuilder();
            IterateChunks(chunks, lyricsBuilder);
        }
        
        return new GeniusSong(artistNames, headerImageUrl, id, isInstrumental, lyricsState,
            songArtImageUrl, title, url, primaryArtistUrl, primaryColor, lyricsBuilder?.ToString());
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
            throw new ObjectDisposedException(nameof(GeniusClient));
        }
    }

    private static void IterateChunks(in JsonElement element, in StringBuilder builder, bool appendStrings = true)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                IterateChunks(child, builder);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            if (appendStrings)
            {
                builder.Append(element.GetString());
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("tag", out var tag))
            {
                string markDownEq = tag.GetString() switch
                {
                    "a" => "",
                    "i" => "*",
                    "b" => "**",
                    _ => "\n"
                };
                builder.Append(markDownEq);
            }

            foreach (var property in element.EnumerateObject())
            {
                IterateChunks(property.Value, builder, false);

                if (property.NameEquals("tag"))
                {
                    string markDownEq = tag.GetString() switch
                    {
                        "i" => "*",
                        "b" => "**",
                        _ => ""
                    };
                    builder.Append(markDownEq);
                }
            }
        }
    }

    /// <inheritdoc/>
    async Task<IEnumerable<IGeniusSong>> IGeniusClient.SearchSongsAsync(string query) => await SearchSongsAsync(query).ConfigureAwait(false);

    /// <inheritdoc/>
    async Task<IGeniusSong?> IGeniusClient.GetSongAsync(int id) => await GetSongAsync(id).ConfigureAwait(false);
}