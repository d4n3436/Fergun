using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Google;

/// <summary>
/// Represents the default Google Lens API client.
/// </summary>
public sealed class GoogleLensClient : IGoogleLensClient, IDisposable
{
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";

    private readonly HttpClient _httpClient;
    private bool _disposed;

    private static ReadOnlySpan<byte> ResultsCallbackStart => Encoding.UTF8.GetBytes("AF_initDataCallback({key: 'ds:0'");
    private static ReadOnlySpan<byte> OcrCallbackStart => Encoding.UTF8.GetBytes("AF_initDataCallback({key: 'ds:1'");
    private static ReadOnlySpan<byte> CallbackEnd => Encoding.UTF8.GetBytes(", sideChannel: {}});</script>");

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleLensClient"/> class.
    /// </summary>
    public GoogleLensClient() : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleLensClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public GoogleLensClient(HttpClient httpClient)
    {
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<string> OcrAsync(string url, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        byte[] page = await _httpClient.GetByteArrayAsync(new Uri($"https://lens.google.com/uploadbyurl?url={Uri.EscapeDataString(url)}"), cancellationToken);

        var data = ExtractDataPack(page, OcrCallbackStart).RootElement[3][4][0];
        if (data.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        return string.Join('\n', data[0].EnumerateArray().Select(x => x.GetString()));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IGoogleLensResult>> ReverseImageSearchAsync(string url, string? language = null, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string requestUrl = $"https://lens.google.com/uploadbyurl?url={Uri.EscapeDataString(url)}";
        if (!string.IsNullOrEmpty(language))
        {
            requestUrl += $"&hl={language}";
        }

        byte[] page = await _httpClient.GetByteArrayAsync(new Uri(requestUrl), cancellationToken);

        var data = ExtractDataPack(page, ResultsCallbackStart).RootElement[1].EnumerateArray().Last()[1][8];

        // No results for this image
        if (data.GetArrayLength() < 9)
            return Array.Empty<IGoogleLensResult>();

        var matches = data[8][0][12];

        return matches.EnumerateArray().Select(x => new GoogleLensResult(
            x[3].GetString()!,
            x[5].GetString()!,
            x[0][0].GetString()!,
            x[7].GetString()!,
            x[15][0].GetString()!)).ToArray();
    }

    private static JsonDocument ExtractDataPack(byte[] page, ReadOnlySpan<byte> callbackStart)
    {
        // Extract the JSON data pack from the page.
        var span = page.AsSpan();

        int callbackStartIndex = span.IndexOf(callbackStart);
        if (callbackStartIndex == -1)
        {
            throw new GoogleLensException("Failed to extract the data pack.");
        }

        int start = span[callbackStartIndex..].IndexOf((byte)'[');
        if (start == -1)
        {
            throw new GoogleLensException("Failed to extract the data pack.");
        }

        start += callbackStartIndex;

        int callbackEndIndex = span[start..].IndexOf(CallbackEnd);
        if (callbackEndIndex == -1)
        {
            throw new GoogleLensException("Failed to extract the data pack.");
        }

        int end = span[..(callbackEndIndex + start)].LastIndexOf((byte)']') + 1;
        if (end == -1)
        {
            throw new GoogleLensException("Failed to extract the data pack.");
        }

        var rawObject = page.AsMemory(start, end - start);

        try
        {
            return JsonDocument.Parse(rawObject);
        }
        catch (JsonException e)
        {
            throw new GoogleLensException("Failed to unpack the image object data.", e);
        }
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
            throw new ObjectDisposedException(nameof(GoogleLensClient));
        }
    }
}