using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a wrapper over Bing Visual Search internal API.
/// </summary>
public sealed class BingVisualSearch : IBingVisualSearch, IDisposable
{
    private const string SKey = "ZbQI4MYyHrlk2E7L-vIV2VLrieGlbMfV8FcK-WCY3ug";
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";

    private static readonly Uri _apiEndpoint = new("https://www.bing.com/images/api/custom/knowledge/");

    private static readonly FrozenDictionary<string, string> _imageCategories = new Dictionary<string, string>(5)
    {
        ["ImageByteSizeExceedsLimit"] = "Image size exceeds the limit (Max. 20MB).",
        ["ImageDimensionsExceedLimit"] = "Image dimensions exceeds the limit (Max. 4000px).",
        ["ImageDownloadFailed"] = "Bing Visual search failed to download the image.",
        ["ServiceUnavailable"] = "Bing Visual search is currently unavailable. Try again later.",
        ["UnknownFormat"] = "Unknown format. Try using JPEG, PNG, or BMP files."
    }.ToFrozenDictionary();

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
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;

        _httpClient.BaseAddress ??= _apiEndpoint;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        }
    }

    /// <inheritdoc/>
    public async Task<string> OcrAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var request = BuildRequest(url, "OCR");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        string? imageCategory = GetImageCategory(document);
        if (imageCategory is not null && _imageCategories.TryGetValue(imageCategory, out string? message))
        {
            throw new BingException(message, imageCategory);
        }

        var ocrTag = GetImageTag(document, "##TextRecognition");

        if (ocrTag.ValueKind == JsonValueKind.Undefined)
            return string.Empty;

        var textRegions = ocrTag
            .GetProperty("actions"u8)[0]
            .GetProperty("data"u8)
            .GetProperty("regions"u8)
            .EnumerateArray()
            .Select(x => string.Join('\n', x.GetProperty("lines"u8).EnumerateArray().Select(y => y.GetProperty("text"u8).GetString())));

        return string.Join("\n\n", textRegions);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IBingReverseImageSearchResult>> ReverseImageSearchAsync(string url,
        BingSafeSearchLevel safeSearch = BingSafeSearchLevel.Moderate, string? language = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var request = BuildRequest(url, "SimilarImages", safeSearch, language);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        string? imageCategory = GetImageCategory(document);
        if (imageCategory is not null && _imageCategories.TryGetValue(imageCategory, out string? message))
        {
            throw new BingException(message, imageCategory);
        }

        var defaultTag = GetImageTag(document, string.Empty);

        if (defaultTag.ValueKind == JsonValueKind.Undefined || !defaultTag.TryGetProperty("actions"u8, out var actions))
            return [];

        var visualSearchAction = actions
            .EnumerateArray()
            .FirstOrDefault(x => x.GetProperty("actionType"u8).ValueEquals("VisualSearch"u8));

        if (visualSearchAction.ValueKind == JsonValueKind.Undefined)
            return [];

        return visualSearchAction
            .GetProperty("data"u8)
            .GetProperty("value"u8)
            .Deserialize<BingReverseImageSearchResult[]>()!
            .AsReadOnly();
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

    private static string? GetImageCategory(JsonDocument document) => document
        .RootElement
        .TryGetProperty("imageQualityHints"u8, out var imageQualityHints)
        ? imageQualityHints[0].GetProperty("category"u8).GetString()
        : null;

    private static JsonElement GetImageTag(JsonDocument document, string displayName) => document
        .RootElement
        .GetProperty("tags"u8)
        .EnumerateArray()
        .FirstOrDefault(x => x.GetProperty("displayName"u8).ValueEquals(displayName));

    private static HttpRequestMessage BuildRequest(string url, string invokedSkill, BingSafeSearchLevel safeSearch = BingSafeSearchLevel.Moderate, string? language = null)
    {
        string jsonRequest = $$$"""{"imageInfo":{"url":"{{{url}}}","source":"Url"},"knowledgeRequest":{"invokedSkills":["{{{invokedSkill}}}"]}}""";
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