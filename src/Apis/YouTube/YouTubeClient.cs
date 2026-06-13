using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.YouTube;

/// <inheritdoc cref="IYouTubeClient" />
public sealed class YouTubeClient : IYouTubeClient, IDisposable
{
    // Protobuf-encoded filter that restricts search results to videos only.
    private const string VideoSearchParams = "EgIQAQ%3D%3D";

    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36";

    private static readonly Uri SearchEndpoint = new("https://www.youtube.com/youtubei/v1/search");
    private static readonly string[] DurationFormats = [@"m\:ss", @"mm\:ss", @"h\:mm\:ss", @"hh\:mm\:ss"];

    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="YouTubeClient"/> class.
    /// </summary>
    public YouTubeClient()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YouTubeClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public YouTubeClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<YouTubeVideo>> SearchVideosAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = new
        {
            query,
            @params = VideoSearchParams,
            context = new
            {
                client = new
                {
                    clientName = "WEB",
                    clientVersion = "2.20210408.08.00",
                    hl = "en",
                    gl = "US",
                    utcOffsetMinutes = 0
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(SearchEndpoint, payload, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        var videos = new List<YouTubeVideo>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var renderer in EnumerateDescendants(document.RootElement, "videoRenderer"))
        {
            string? id = renderer.TryGetProperty("videoId"u8, out var videoId) ? videoId.GetString() : null;

            if (string.IsNullOrEmpty(id) || !seenIds.Add(id))
            {
                continue;
            }

            videos.Add(new YouTubeVideo(id, GetTitle(renderer), GetAuthor(renderer), GetDuration(renderer)));
        }

        return videos.AsReadOnly();
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

    /// <summary>
    /// Recursively enumerates all properties with the specified name within a JSON element.
    /// </summary>
    private static IEnumerable<JsonElement> EnumerateDescendants(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(propertyName))
                    {
                        yield return property.Value;
                    }

                    foreach (var descendant in EnumerateDescendants(property.Value, propertyName))
                    {
                        yield return descendant;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var descendant in EnumerateDescendants(item, propertyName))
                    {
                        yield return descendant;
                    }
                }

                break;
        }
    }

    private static string GetTitle(JsonElement renderer)
    {
        if (!renderer.TryGetProperty("title"u8, out var title))
        {
            return string.Empty;
        }

        return title.TryGetProperty("simpleText"u8, out var simpleText)
            ? simpleText.GetString() ?? string.Empty
            : ConcatRuns(title);
    }

    private static string GetAuthor(JsonElement renderer)
    {
        if (renderer.TryGetProperty("longBylineText"u8, out var byline) || renderer.TryGetProperty("shortBylineText"u8, out byline))
        {
            return GetFirstRunText(byline);
        }

        return string.Empty;
    }

    private static TimeSpan? GetDuration(JsonElement renderer)
    {
        if (!renderer.TryGetProperty("lengthText"u8, out var lengthText))
        {
            return null;
        }

        string text = lengthText.TryGetProperty("simpleText"u8, out var simpleText)
            ? simpleText.GetString() ?? string.Empty
            : ConcatRuns(lengthText);

        return TimeSpan.TryParseExact(text, DurationFormats, CultureInfo.InvariantCulture, out var duration)
            ? duration
            : null;
    }
    
    private static string GetFirstRunText(JsonElement container)
    {
        if (container.TryGetProperty("runs"u8, out var runs) && runs.ValueKind == JsonValueKind.Array)
        {
            foreach (var run in runs.EnumerateArray())
            {
                return run.TryGetProperty("text"u8, out var text) ? text.GetString() ?? string.Empty : string.Empty;
            }
        }

        return string.Empty;
    }
    
    private static string ConcatRuns(JsonElement container)
    {
        if (!container.TryGetProperty("runs"u8, out var runs) || runs.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var run in runs.EnumerateArray())
        {
            if (run.TryGetProperty("text"u8, out var text))
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }
}