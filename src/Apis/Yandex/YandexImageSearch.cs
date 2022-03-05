using System.Text.Json;
using Fergun.Extensions;

namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a wrapper over Yandex Image Search internal API.
/// </summary>
public sealed class YandexImageSearch : IDisposable
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

    /// <summary>
    /// Performs OCR to the specified image URL.
    /// </summary>
    /// <param name="url">The URL of an image.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous OCR operation. The result contains the recognized text.</returns>
    public async Task<string> OcrAsync(string url)
    {
        // Get CBIR ID

        using var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://yandex.com/images-apphost/image-download?url={Uri.EscapeDataString(url)}&cbird=37&images_avatars_size=orig&images_avatars_namespace=images-cbir")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        string imageId = document
            .RootElement
            .GetProperty("image_id")
            .GetString() ?? throw new YandexException("Unable to get the ID of the image.");

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

        await using var ocrStream = await ocrResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var ocrDocument = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        return ocrDocument
            .RootElement
            .GetProperty("blocks")[0]
            .GetProperty("params")
            .GetPropertyOrDefault("adapterData")
            .GetPropertyOrDefault("plainText")
            .GetStringOrDefault() ?? "";
    }

    /// <inheritdoc/>
    public void Dispose() => Dispose(true);

    /// <inheritdoc cref="Dispose()"/>
    private void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}