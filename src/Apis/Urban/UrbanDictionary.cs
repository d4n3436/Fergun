using System.Text.Json;

namespace Fergun.Apis.Urban;

/// <summary>
/// Represents an API wrapper for Urban Dictionary.
/// </summary>
public sealed class UrbanDictionary : IDisposable, IUrbanDictionary
{
    private static readonly Uri _apiEndpoint = new("https://api.urbandictionary.com/v0/");

    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UrbanDictionary"/> class.
    /// </summary>
    public UrbanDictionary()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UrbanDictionary"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public UrbanDictionary(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress ??= _apiEndpoint;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanDefinition>> GetDefinitionsAsync(string term)
    {
        EnsureNotDisposed();
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"define?term={Uri.EscapeDataString(term)}", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IReadOnlyList<UrbanDefinition>>()!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanDefinition>> GetRandomDefinitionsAsync()
    {
        EnsureNotDisposed();
        await using var stream = await _httpClient.GetStreamAsync(new Uri("random", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IReadOnlyList<UrbanDefinition>>()!;
    }

    /// <inheritdoc/>
    public async Task<UrbanDefinition?> GetDefinitionAsync(int id)
    {
        EnsureNotDisposed();
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"define?defid={id}", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var list = document.RootElement.GetProperty("list");

        return list.GetArrayLength() == 0 ? null : list[0].Deserialize<UrbanDefinition>()!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanDefinition>> GetWordsOfTheDayAsync()
    {
        EnsureNotDisposed();
        await using var stream = await _httpClient.GetStreamAsync(new Uri("words_of_the_day", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IReadOnlyList<UrbanDefinition>>()!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string term)
    {
        EnsureNotDisposed();
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"autocomplete?term={Uri.EscapeDataString(term)}", UriKind.Relative)).ConfigureAwait(false);
        return (await JsonSerializer.DeserializeAsync<IReadOnlyList<string>>(stream).ConfigureAwait(false))!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanAutocompleteResult>> GetAutocompleteResultsExtraAsync(string term)
    {
        EnsureNotDisposed();
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"autocomplete-extra?term={Uri.EscapeDataString(term)}", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("results").Deserialize<IReadOnlyList<UrbanAutocompleteResult>>()!;
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
            throw new ObjectDisposedException(nameof(UrbanDictionary));
        }
    }
}