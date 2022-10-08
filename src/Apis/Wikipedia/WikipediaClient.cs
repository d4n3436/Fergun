using System.Text.Json;

namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a Wikipedia API client.
/// </summary>
public sealed class WikipediaClient : IWikipediaClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikipediaClient"/> class.
    /// </summary>
    public WikipediaClient()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WikipediaClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public WikipediaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IWikipediaArticle>> GetArticlesAsync(string query, string language, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string url = $"https://{language}.wikipedia.org/w/api.php?" +
                     "action=query" +
                     "&generator=prefixsearch" + // https://www.mediawiki.org/wiki/API:Prefixsearch
                     "&format=json" +
                     "&formatversion=2" +
                     "&prop=extracts|pageimages|description" + // Get article extract, page images and short description
                     "&exintro" + // Return only content before the first section
                     "&explaintext" + // Return extracts as plain text
                     "&redirects" + // Automatically resolve redirects
                     $"&gpssearch={Uri.EscapeDataString(query)}" + // Search string
                     "&pilicense=any" + // Get images with any license
                     "&piprop=original"; // Get original images

        var response = await _httpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("query", out var queryProp))
        {
            return Enumerable.Empty<WikipediaArticle>();
        }

        return queryProp
            .GetProperty("pages")
            .EnumerateArray()
            .OrderBy(x => x.GetProperty("index").GetInt32())
            .Select(x => x.Deserialize<WikipediaArticle>()!);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string query, string language, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        using var response = await _httpClient.GetAsync(new Uri($"https://{language}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&redirects=resolve"),
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        return document
            .RootElement[1]
            .Deserialize<IReadOnlyList<string>>()!;
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
            throw new ObjectDisposedException(nameof(WikipediaClient));
        }
    }
}