using System.Text.Json;
using Fergun.Extensions;

namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a wrapper over Bing Visual Search internal API.
/// </summary>
public sealed class BingVisualSearch : IBingVisualSearch, IDisposable
{
    private static readonly Uri _apiEndpoint = new("https://www.bing.com/images/api/custom/knowledge/");

    private static readonly Dictionary<string, string> _imageCategories = new()
    {
        ["ImageByteSizeExceedsLimit"] = "Image size exceeds the limit (Max. 20MB)",
        ["ImageDimensionsExceedLimit"] = "Image dimensions exceeds the limit (Max. 4000px)",
        ["ImageDownloadFailed"] = "Image download failed",
        ["ServiceUnavailable"] = "Bing Visual search is currently unavailable. Try again later",
        ["UnknownFormat"] = "Unknown format (Only JPEG, PNG or BMP allowed)."
    };

    private const string _sKey = "ZbQI4MYyHrlk2E7L-vIV2VLrieGlbMfV8FcK-WCY3ug";
    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BingVisualSearch"/> class.
    /// </summary>
    public BingVisualSearch()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BingVisualSearch"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public BingVisualSearch(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress ??= _apiEndpoint;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> OcrAsync(string url)
    {
        EnsureNotDisposed();
        using var request = BuildRequest(url, "OCR");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        string? imageCategory = document
            .RootElement
            .GetPropertyOrDefault("imageQualityHints")
            .FirstOrDefault()
            .GetPropertyOrDefault("category")
            .GetStringOrDefault();

        if (imageCategory is not null && _imageCategories.TryGetValue(imageCategory, out var message))
        {
            throw new BingException(message);
        }

        var textRegions = document
            .RootElement
            .GetProperty("tags")
            .FirstOrDefault(x => x.GetPropertyOrDefault("displayName").GetStringOrDefault() == "##TextRecognition")
            .GetPropertyOrDefault("actions")
            .FirstOrDefault()
            .GetPropertyOrDefault("data")
            .GetPropertyOrDefault("regions")
            .EnumerateArrayOrEmpty()
            .Select(x => string.Join('\n',
                x.GetPropertyOrDefault("lines")
                    .EnumerateArrayOrEmpty()
                    .Select(y => y.GetPropertyOrDefault("text").GetStringOrDefault())));

        return string.Join("\n\n", textRegions);
    }

    /// <inheritdoc cref="IBingVisualSearch.ReverseImageSearchAsync(string, bool)"/>
    public async Task<IEnumerable<BingReverseImageSearchResult>> ReverseImageSearchAsync(string url, bool onlyFamilyFriendly)
    {
        EnsureNotDisposed();
        using var request = BuildRequest(url, "SimilarImages");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var root = document.RootElement.Clone();

        var rawItems = root
            .GetProperty("tags")
            .EnumerateArray()
            .Select(x => x.GetPropertyOrDefault("actions"))
            .SelectMany(x => x.EnumerateArrayOrEmpty())
            .FirstOrDefault(x => x.GetPropertyOrDefault("actionType").GetStringOrDefault() == "VisualSearch")
            .GetPropertyOrDefault("data")
            .GetPropertyOrDefault("value")
            .EnumerateArrayOrEmpty();

        return EnumerateResults(rawItems, onlyFamilyFriendly);
    }

    private static IEnumerable<BingReverseImageSearchResult> EnumerateResults(IEnumerable<JsonElement> rawItems, bool onlyFamilyFriendly)
    {
        foreach (var item in rawItems)
        {
            if (onlyFamilyFriendly && item.GetPropertyOrDefault("isFamilyFriendly").ValueKind == JsonValueKind.False)
                continue;

            yield return item.Deserialize<BingReverseImageSearchResult>()!;
        }
    }

    private static HttpRequestMessage BuildRequest(string url, string invokedSkill)
    {
        string jsonRequest = $"{{\"imageInfo\":{{\"url\":\"{url}\",\"source\":\"Url\"}},\"knowledgeRequest\":{{\"invokedSkills\":[\"{invokedSkill}\"]}}}}";
        var content = new MultipartFormDataContent
        {
            { new StringContent(jsonRequest), "knowledgeRequest" }
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"?skey={_sKey}", UriKind.Relative),
            Content = content
        };

        request.Headers.Referrer = new Uri($"https://www.bing.com/images/search?view=detailv2&iss=sbi&q=imgurl:{url}");

        return request;
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
            throw new ObjectDisposedException(nameof(BingVisualSearch));
        }
    }

    /// <inheritdoc/>
    async Task<IEnumerable<IBingReverseImageSearchResult>> IBingVisualSearch.ReverseImageSearchAsync(string url, bool onlyFamilyFriendly)
        => await ReverseImageSearchAsync(url, onlyFamilyFriendly).ConfigureAwait(false);
}