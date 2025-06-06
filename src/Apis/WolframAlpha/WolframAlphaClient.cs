﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents the default WolframAlpha client.
/// </summary>
public sealed class WolframAlphaClient : IWolframAlphaClient, IDisposable
{
    private const string DefaultUserAgent = "Wolfram Alpha Classic Android App 1.4.25.20250523525";

    // This AppID is the result of an algorithm in the Android app that takes 2 byte arrays,
    // the first one comes from the values of the resource keys "app_one_id", "id_2_app", "appid_three", "four_appid"
    // and the other array is the MD5 hash of assets\close_dont_change.png (3AC2AA8E493A260B877A68AFC5D1F9F4), also inside the app.
    private const string AppId = "Y5H46L-2KR8T4PPQQ";

    // The secret key is also the result of the same algorithm using the values of the resource keys "secret_1_key", "key_secret_2", "three_secret_key", "four_secret_key"
    private const string SecretKey = "EumRuvaOhx7ENr9N";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WolframAlphaClient"/> class.
    /// </summary>
    public WolframAlphaClient()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WolframAlphaClient"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public WolframAlphaClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAutocompleteResultsAsync(string input, string language, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);
        ArgumentException.ThrowIfNullOrEmpty(language);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = await _httpClient.GetStreamAsync(new Uri($"https://{GetSubdomain(language)}.wolframalpha.com/n/v1/api/autocomplete/?appid={AppId}&i={Uri.EscapeDataString(input)}"), cancellationToken).ConfigureAwait(false);

        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        return document
            .RootElement
            .GetProperty("results"u8)
            .EnumerateArray()
            .Select(x => x.GetProperty("input"u8).GetString()!)
            .ToArray()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IWolframAlphaResult> SendQueryAsync(string input, string language, bool reinterpret = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input); // Allow empty string for testing
        ArgumentException.ThrowIfNullOrEmpty(language);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        string escapedInput = Uri.EscapeDataString(input);

        // The key-value pairs must be ordered like tuples would
        byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{SecretKey}appid{AppId}input{escapedInput}outputjsonreinterpret{reinterpret}"));
        string signature = Convert.ToHexString(bytes);

        await using var stream = await _httpClient.GetStreamAsync(new Uri($"https://api.wolframalpha.com/v2/query.jsp?appid={AppId}&input={escapedInput}&reinterpret={reinterpret}&output=json&sig={signature}"), cancellationToken).ConfigureAwait(false);

        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        return document.RootElement.GetProperty("queryresult"u8).Deserialize<WolframAlphaResult>()!;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _disposed = true;
    }

    private static string GetSubdomain(string language)
        => language switch
        {
            "es" => "es",
            "ja" => "ja",
            _ => "www"
        };
}