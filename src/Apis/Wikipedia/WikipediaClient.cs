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
    public async Task<IWikipediaArticle?> GetArticleAsync(int id, string language, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string url = $"https://{language}.wikipedia.org/w/api.php?" +
                     "action=query" +
                     $"&pageids={id}" +
                     "&format=json" +
                     "&formatversion=2" +
                     "&prop=extracts|pageimages|description" + // Get article extract, page images and short description
                     "&exintro" + // Return only content before the first section
                     "&explaintext" + // Return extracts as plain text
                     "&redirects" + // Automatically resolve redirects
                     "&pilicense=any" + // Get images with any license
                     "&piprop=original"; // Get original images

        using var response = await _httpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        var page = document.RootElement
            .GetProperty("query")
            .GetProperty("pages")[0];

        if (page.TryGetProperty("missing", out var missing) && missing.GetBoolean())
        {
            return null;
        }

        return page.Deserialize<WikipediaArticle>()!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IPartialWikipediaArticle>> SearchArticlesAsync(string query, string language, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string url = $"https://{language}.wikipedia.org/w/api.php?action=query&list=search&srsearch=intitle:{Uri.EscapeDataString(query)}&utf8&format=json&srprop=";
        using var response = await _httpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        return document
            .RootElement
            .GetProperty("query")
            .GetProperty("search")
            .Deserialize<IReadOnlyList<PartialWikipediaArticle>>()!;
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