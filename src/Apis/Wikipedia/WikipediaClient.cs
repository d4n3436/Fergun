using System.Text.Json;
using Fergun.Extensions;

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

    /// <inheritdoc cref="IWikipediaClient.GetArticlesAsync(string, string)"/>
    public async Task<IEnumerable<WikipediaArticle>> GetArticlesAsync(string query, string language)
    {
        EnsureNotDisposed();

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

        var response = await _httpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        return document
            .RootElement
            .GetPropertyOrDefault("query")
            .GetPropertyOrDefault("pages")
            .EnumerateArrayOrEmpty()
            .OrderBy(x => x.GetProperty("index").GetInt32())
            .Select(x => x.Deserialize<WikipediaArticle>()!);
    }

    /// <summary>
    /// Gets autocomplete results.
    /// </summary>
    /// <param name="query">The search string.</param>
    /// <param name="language">The search language.</param>
    /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation. The result contains a read-only list of autocomplete results.</returns>
    public async Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string query, string language)
    {
        EnsureNotDisposed();

        var response = await _httpClient.GetAsync(new Uri($"https://{language}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&redirects=resolve"), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        return document
            .RootElement
            .EnumerateArray()
            .ElementAt(1)
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

    /// <inheritdoc/>
    async Task<IEnumerable<IWikipediaArticle>> IWikipediaClient.GetArticlesAsync(string query, string language)
        => await GetArticlesAsync(query, language).ConfigureAwait(false);
}