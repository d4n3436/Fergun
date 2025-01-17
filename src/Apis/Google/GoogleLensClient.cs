﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Google;

/// <summary>
/// Represents the default Google Lens API client.
/// </summary>
public sealed class GoogleLensClient : IGoogleLensClient, IDisposable
{
    private const string DefaultUserAgent = "Mozilla/5.0 (Linux; Android 14; Google Pixel 9 Pro XL; 3854511) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36";

    private readonly HttpClient _httpClient;
    private bool _disposed;

    private static ReadOnlySpan<byte> ResultsCallbackStart => "AF_initDataCallback({key: 'ds:0'"u8;
    private static ReadOnlySpan<byte> OcrCallbackStart => "AF_initDataCallback({key: 'ds:1'"u8;
    private static ReadOnlySpan<byte> CallbackEnd => ", sideChannel: {}});</script>"u8;

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
        ArgumentNullException.ThrowIfNull(url);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] page = await _httpClient.GetByteArrayAsync(new Uri($"https://lens.google.com/uploadbyurl?url={Uri.EscapeDataString(url)}"), cancellationToken).ConfigureAwait(false);

        var data = ExtractDataPack(page, OcrCallbackStart).RootElement[3][4][0];
        if (data.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        return string.Join('\n', data[0].Deserialize<string[]>()!);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IGoogleLensResult>> ReverseImageSearchAsync(string url, string? language = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        string requestUrl = $"https://lens.google.com/uploadbyurl?url={Uri.EscapeDataString(url)}";
        if (!string.IsNullOrEmpty(language))
        {
            requestUrl += $"&hl={language}";
        }

        byte[] page = await _httpClient.GetByteArrayAsync(new Uri(requestUrl), cancellationToken).ConfigureAwait(false);

        var data = ExtractDataPack(page, ResultsCallbackStart).RootElement[1].EnumerateArray().Last()[1][8];

        // No results for this image
        if (data.GetArrayLength() < 9)
            return [];

        var matches = data[8][0][12];

        return matches
            .EnumerateArray()
            .Where(x => x[0].GetArrayLength() != 0)
            .Select(x => new GoogleLensResult(
            x[3].GetString()!,
            x[5].GetString()!,
            x[0][0].GetString()!,
            x[7].GetString()!,
            x[15][0].GetString()!))
            .ToArray();
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
}