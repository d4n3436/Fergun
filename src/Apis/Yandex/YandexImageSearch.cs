using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Fergun.Extensions;

namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a wrapper over Yandex Image Search internal API.
/// </summary>
public sealed class YandexImageSearch : IYandexImageSearch, IDisposable
{
    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";
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
        _httpClient = httpClient;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> OcrAsync(string url)
    {
        EnsureNotDisposed();

        // Get CBIR ID
        using var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://yandex.com/images-apphost/image-download?url={Uri.EscapeDataString(url)}&cbird=37&images_avatars_size=orig&images_avatars_namespace=images-cbir")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new YandexException(message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        string? imageId = document
            .RootElement
            .GetProperty("image_id")
            .GetString();

        int imageShard = document
            .RootElement
            .GetProperty("image_shard")
            .GetInt32();

        // Get OCR text
        const string ocrJsonRequest = @"{""blocks"":[{""block"":{""block"":""i-react-ajax-adapter:ajax""},""params"":{""type"":""CbirOcr""},""version"":2}]}";

        using var ocrRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://yandex.com/images/search?format=json&request={ocrJsonRequest}&rpt=ocr&cbir_id={imageShard}/{imageId}")
        };

        using var ocrResponse = await _httpClient.SendAsync(ocrRequest, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        ocrResponse.EnsureSuccessStatusCode();

        // Using an stream here causes Parse(Async) to throw a JsonReaderException for some reason
        var bytes = await ocrResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        using var ocrDocument = JsonDocument.Parse(bytes);

        if (ocrDocument.RootElement.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "captcha")
        {
            throw new YandexException("Yandex API returned a CAPTCHA. Try again later.");
        }

        return ocrDocument
            .RootElement
            .GetProperty("blocks")[0]
            .GetProperty("params")
            .GetPropertyOrDefault("adapterData")
            .GetPropertyOrDefault("plainText")
            .GetStringOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IYandexReverseImageSearchResult>> ReverseImageSearchAsync(string url, YandexSearchFilterMode mode = YandexSearchFilterMode.Moderate)
    {
        const string imageSearchRequest = @"{""blocks"":[{""block"":""content_type_similar"",""params"":{},""version"":2}]}";

        using var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://yandex.com/images/search?rpt=imageview&url={Uri.EscapeDataString(url)}&cbir_page=similar&format=json&request={imageSearchRequest}")
        };

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

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        if (document.RootElement.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "captcha")
        {
            throw new YandexException("Yandex API returned a CAPTCHA. Try again later.");
        }

        string html = document
            .RootElement
            .GetProperty("blocks")[0]
            .GetProperty("html")
            .GetString() ?? string.Empty;

        var htmlDocument = _parser.ParseDocument(html);

        var rawItems = htmlDocument
            .GetElementsByClassName("serp-list")
            .FirstOrDefault()?
            .GetElementsByClassName("serp-item")
            .Select(x => x.GetAttribute("data-bem")) ?? Enumerable.Empty<string?>();

        return EnumerateResults(rawItems);
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

    private static IEnumerable<YandexReverseImageSearchResult> EnumerateResults(IEnumerable<string?> rawItems)
    {
        foreach (string? rawItem in rawItems)
        {
            if (string.IsNullOrEmpty(rawItem))
                continue;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(rawItem);
            }
            catch
            {
                continue;
            }

            var item = document.RootElement.GetPropertyOrDefault("serp-item");
            var snippet = item.GetPropertyOrDefault("snippet");

            var url = item
                //.GetPropertyOrDefault("dups")
                //.LastOrDefault()
                //.GetPropertyOrDefault("url")
                //.GetStringOrDefault() ?? item
                .GetPropertyOrDefault("img_href")
                .GetStringOrDefault();

            var sourceUrl = snippet.GetPropertyOrDefault("url").GetStringOrDefault();
            var title = snippet.GetPropertyOrDefault("title").GetStringOrDefault();
            var text = snippet.GetPropertyOrDefault("text").GetStringOrDefault();

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(text))
            {
                continue;
            }

            yield return new YandexReverseImageSearchResult(url, sourceUrl, WebUtility.HtmlDecode(title), text);
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(YandexImageSearch));
        }
    }
}