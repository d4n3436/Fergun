using System.Text.Json;
using Fergun.Extensions;

namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a wrapper over Yandex Image Search internal API.
/// </summary>
public sealed class YandexImageSearch : IYandexImageSearch, IDisposable
{
    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="YandexImageSearch"/> class.
    /// </summary>
    public YandexImageSearch()
        : this(new HttpClient())
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
            throw new ObjectDisposedException(nameof(YandexImageSearch));
        }
    }
}