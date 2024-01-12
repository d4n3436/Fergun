using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Urban;

/// <summary>
/// Represents an API wrapper for Urban Dictionary.
/// </summary>
public sealed class UrbanDictionary : IDisposable, IUrbanDictionary
{
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";

    private static readonly Uri _apiEndpoint = new("https://api.urbandictionary.com/v0/");

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
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanDefinition>> GetDefinitionsAsync(string term, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await _httpClient.GetStreamAsync(new Uri($"define?term={Uri.EscapeDataString(term)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IReadOnlyList<UrbanDefinition>>()!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanDefinition>> GetRandomDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await _httpClient.GetStreamAsync(new Uri("random", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IReadOnlyList<UrbanDefinition>>()!;
    }

    /// <inheritdoc/>
    public async Task<UrbanDefinition?> GetDefinitionAsync(int id, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await _httpClient.GetStreamAsync(new Uri($"define?defid={id}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
        var list = document.RootElement.GetProperty("list");

        return list.GetArrayLength() == 0 ? null : list[0].Deserialize<UrbanDefinition>()!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanDefinition>> GetWordsOfTheDayAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await _httpClient.GetStreamAsync(new Uri("words_of_the_day", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IReadOnlyList<UrbanDefinition>>()!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string term, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        return (await _httpClient.GetFromJsonAsync<IReadOnlyList<string>>(new Uri($"autocomplete?term={Uri.EscapeDataString(term)}", UriKind.Relative), cancellationToken).ConfigureAwait(false))!;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UrbanAutocompleteResult>> GetAutocompleteResultsExtraAsync(string term, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await _httpClient.GetStreamAsync(new Uri($"autocomplete-extra?term={Uri.EscapeDataString(term)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
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
}