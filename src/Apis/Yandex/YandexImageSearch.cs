using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;

namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a wrapper over Yandex Image Search internal API.
/// </summary>
public sealed class YandexImageSearch : IYandexImageSearch, IDisposable
{
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";
    private static readonly HtmlParser _parser = new();
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="YandexImageSearch"/> class.
    /// </summary>
    public YandexImageSearch()
        : this(new HttpClient(new HttpClientHandler { UseCookies = false }))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YandexImageSearch"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public YandexImageSearch(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> OcrAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Get CBIR ID
        var uri = new Uri($"https://yandex.com/images-apphost/image-download?url={Uri.EscapeDataString(url)}&cbird=37&images_avatars_size=orig&images_avatars_namespace=images-cbir");

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            string message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new YandexException(message, e);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        string? imageId = document
            .RootElement
            .GetProperty("image_id"u8)
            .GetString();

        int imageShard = document
            .RootElement
            .GetProperty("image_shard"u8)
            .GetInt32();

        // Get OCR text
        const string ocrJsonRequest = """{"blocks":[{"block":{"block":"i-react-ajax-adapter:ajax"},"params":{"type":"CbirOcr","subtype":"legacy"},"version":2}]}""";

        var ocrRequestUri = new Uri($"https://yandex.com/images/search?format=json&request={ocrJsonRequest}&rpt=ocr&cbir_id={imageShard}/{imageId}");
        using var ocrResponse = await _httpClient.GetAsync(ocrRequestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        ocrResponse.EnsureSuccessStatusCode();

        // A byte array is used because SendAsync returns a chunked response and the Stream from ReadAsStreamAsync is not seekable.
        byte[] bytes = await ocrResponse.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        using var ocrDocument = JsonDocument.Parse(bytes);

        if (ocrDocument.RootElement.TryGetProperty("type"u8, out var type) && type.ValueEquals("captcha"u8))
        {
            throw new YandexException("Yandex API returned a CAPTCHA. Try again later.");
        }

        return ocrDocument
            .RootElement
            .GetProperty("blocks"u8)[0] // There should be a single block, "i-react-ajax-adapter:ajax"
            .GetProperty("params"u8)
            .GetProperty("adapterData"u8)
            .GetProperty("plainText"u8)
            .GetString();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IYandexReverseImageSearchResult>> ReverseImageSearchAsync(string url,
        YandexSearchFilterMode mode = YandexSearchFilterMode.Moderate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        const string imageSearchRequest = """{"blocks":[{"block":"content_type_similar","params":{},"version":2}]}""";

        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Get;
        request.RequestUri = new Uri($"https://yandex.com/images/search?rpt=imageview&url={Uri.EscapeDataString(url)}&cbir_page=similar&format=json&request={imageSearchRequest}");

        var now = DateTimeOffset.UtcNow;

        string? yp = mode switch
        {
            YandexSearchFilterMode.None => $"{now.AddYears(10).AddDays(7).ToUnixTimeSeconds()}.sp.aflt%3A{now.ToUnixTimeSeconds()}#{now.AddDays(7).ToUnixTimeSeconds()}.szm.1%3A1920x1080%3A1272x969",
            YandexSearchFilterMode.Family => $"{now.AddYears(10).AddDays(7).ToUnixTimeSeconds()}.sp.family%3A2#{now.AddDays(7).ToUnixTimeSeconds()}.szm.1%3A1920x1080%3A1272x969",
            _ => null
        };

        if (yp is not null)
        {
            request.Headers.Add("Cookie", $"yp={yp}");
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("type"u8, out var type) && type.ValueEquals("captcha"u8))
        {
            throw new YandexException("Yandex API returned a CAPTCHA. Try again later.");
        }

        string html = document
            .RootElement
            .GetProperty("blocks"u8)[0]
            .GetProperty("html"u8)
            .GetString()!;

        var htmlDocument = await _parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);

        string? json = htmlDocument
            .GetElementsByClassName("cbir-similar-page").FirstOrDefault()?
            .GetElementsByClassName("cbir-similar-page__content").FirstOrDefault()?
            .GetElementsByClassName("Root").FirstOrDefault()?
            .GetAttribute("data-state") ?? htmlDocument
            .GetElementsByClassName("Root").FirstOrDefault()?
            .GetAttribute("data-state");

        if (json is null)
        {
            return [];
        }

        using var data = JsonDocument.Parse(json);

        if (data.RootElement.TryGetProperty("initialState"u8, out var initialState))
        {
            return initialState
                .GetProperty("serpList"u8)
                .GetProperty("items"u8)
                .GetProperty("entities"u8)
                .EnumerateObject()
                .Select(x => x.Value.GetProperty("viewerData"u8).Deserialize<YandexReverseImageSearchResult>()!)
                .ToArray();
        }

        // Old layout
        return htmlDocument
            .GetElementsByClassName("serp-list")
            .FirstOrDefault()?
            .GetElementsByClassName("serp-item")
            .Select(x => JsonDocument.Parse(x.GetAttribute("data-bem")!).RootElement.GetProperty("serp-item"u8).Deserialize<YandexReverseImageSearchResult>()!)
            .ToArray() ?? [];
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