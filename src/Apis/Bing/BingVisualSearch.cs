using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fergun.Extensions;

namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a wrapper over Bing Visual Search internal API.
/// </summary>
public sealed class BingVisualSearch : IBingVisualSearch, IDisposable
{
    private const string SKey = "ZbQI4MYyHrlk2E7L-vIV2VLrieGlbMfV8FcK-WCY3ug";
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";

    private static readonly Uri _apiEndpoint = new("https://www.bing.com/images/api/custom/knowledge/");

    private static readonly Dictionary<string, string> _imageCategories = new(5)
    {
        ["ImageByteSizeExceedsLimit"] = "Image size exceeds the limit (Max. 20MB).",
        ["ImageDimensionsExceedLimit"] = "Image dimensions exceeds the limit (Max. 4000px).",
        ["ImageDownloadFailed"] = "Bing Visual search failed to download the image.",
        ["ServiceUnavailable"] = "Bing Visual search is currently unavailable. Try again later.",
        ["UnknownFormat"] = "Unknown format. Try using JPEG, PNG, or BMP files."
    };

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
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IBingReverseImageSearchResult>> ReverseImageSearchAsync(string url,
        BingSafeSearchLevel safeSearch = BingSafeSearchLevel.Moderate, string? language = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var request = BuildRequest(url, "SimilarImages", safeSearch, language);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        string? imageCategory = document
            .RootElement
            .GetPropertyOrDefault("imageQualityHints")
            .FirstOrDefault()
            .GetPropertyOrDefault("category")
            .GetStringOrDefault();

        if (imageCategory is not null && _imageCategories.TryGetValue(imageCategory, out string? message))
        {
            throw new BingException(message, imageCategory);
        }

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

        return rawItems.Select(item => item.Deserialize<BingReverseImageSearchResult>()!);
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

    private static HttpRequestMessage BuildRequest(string url, string invokedSkill, BingSafeSearchLevel safeSearch = BingSafeSearchLevel.Moderate, string? language = null)
    {
        string jsonRequest = $"{{\"imageInfo\":{{\"url\":\"{url}\",\"source\":\"Url\"}},\"knowledgeRequest\":{{\"invokedSkills\":[\"{invokedSkill}\"]}}}}";
        var content = new MultipartFormDataContent
        {
            { new StringContent(jsonRequest), "knowledgeRequest" }
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"?skey={SKey}&safeSearch={safeSearch}{(language is null ? string.Empty : $"&setLang={language}")}", UriKind.Relative),
            Content = content
        };

        request.Headers.Referrer = new Uri($"https://www.bing.com/images/search?view=detailv2&iss=sbi&q=imgurl:{url}");

        return request;
    }
}