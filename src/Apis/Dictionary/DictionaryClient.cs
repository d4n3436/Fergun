using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

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
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IDictionaryWord>> GetSearchResultsAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        IDictionarySearchResponse response = (await _httpClient.GetFromJsonAsync<DictionarySearchResponse>(
            new Uri($"https://thor-graphql.dictionary.com/v2/search?searchText={Uri.EscapeDataString(text)}"),
            cancellationToken).ConfigureAwait(false))!;

        return response.Data;
    }

    /// <inheritdoc/>
    public async Task<IDictionaryResponse> GetDefinitionsAsync(string word, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(word);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var response = await _httpClient.GetAsync(new Uri($"https://mobile-api.dictionary.com/1/dictionary/data/full?slug={Uri.EscapeDataString(word)}&context=dcom"), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }

        return (await response.Content.ReadFromJsonAsync<DictionaryResponse>(cancellationToken).ConfigureAwait(false))!;
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
}