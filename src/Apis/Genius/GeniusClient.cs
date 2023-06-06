using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

        var model = await _httpClient.GetFromJsonAsync<GeniusResponse<GeniusSearchResponse>>(new Uri($"search?q={Uri.EscapeDataString(query)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);

        return Array.AsReadOnly(model!.Response.Hits.Select(x => x.Result).ToArray());
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
        return (await response.Content.ReadFromJsonAsync<GeniusResponse<GeniusSongResponse>>(JsonSerializerOptions.Default, cancellationToken).ConfigureAwait(false))!.Response.Song;
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
}