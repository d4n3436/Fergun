using System.Net.Http.Json;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents a wrapper for the Dictionary.com internal API.
/// </summary>
public sealed class DictionaryClient : IDictionaryClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryClient"/> class.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public DictionaryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IDictionaryWord>> GetSearchResultsAsync(string text, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        var response = (await _httpClient.GetFromJsonAsync<DictionarySearchResponse>(new Uri($"https://thor-graphql.dictionary.com/v2/search?searchText={Uri.EscapeDataString(text)}"), cancellationToken).ConfigureAwait(false))!;
        return response.Data;
    }

    /// <inheritdoc/>
    public async Task<IDictionaryResponse> GetDefinitionsAsync(string word, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        return (await _httpClient.GetFromJsonAsync<DictionaryResponse>(new Uri($"https://api-portal.dictionary.com/dcom/pageData/{Uri.EscapeDataString(word)}"), cancellationToken).ConfigureAwait(false))!;
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
            throw new ObjectDisposedException(nameof(DictionaryClient));
        }
    }
}