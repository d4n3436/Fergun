using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Fergun.Apis.Google;

/// <summary>
/// Represents the default Google Lens API client.
/// </summary>
public sealed class GoogleLensClient : IGoogleLensClient, IDisposable
{
    private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;
    private bool _disposed;

    private static ReadOnlySpan<byte> ImageResultsStart => "(function(){var m="u8;
    private static ReadOnlySpan<byte> ImageResultsEnd => ";var a=m;"u8;

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

        var response = await _httpClient.GetAsync(new Uri($"https://lens.google.com/uploadbyurl?url={Uri.EscapeDataString(url)}"), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        var parameters = HttpUtility.ParseQueryString(response.RequestMessage!.RequestUri!.Query);
        string? vsrid = parameters["vsrid"];
        string? gsessionid = parameters["gsessionid"];

        if (string.IsNullOrEmpty(vsrid) || string.IsNullOrEmpty(gsessionid))
        {
            throw new GoogleLensException("Failed to extract the required query parameters.");
        }

        ReadOnlyMemory<byte> bytes = await _httpClient.GetByteArrayAsync(new Uri($"https://lens.google.com/qfmetadata?vsrid={vsrid}&gsessionid={gsessionid}"), cancellationToken).ConfigureAwait(false);
        var document = JsonDocument.Parse(bytes[6..]); // Skip magic chars

        var root = document.RootElement[0][2];
        if (root[0].ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var textParagraphs = root[0][0];

        foreach (var paragraph in textParagraphs.EnumerateArray())
        {
            var lines = paragraph[1];
            foreach (var line in lines.EnumerateArray())
            {
                var tokens = line[0];
                foreach (var token in tokens.EnumerateArray())
                {
                    builder.Append(token[1]);
                    builder.Append(token[2]);
                }

                builder.Append('\n');
            }

            builder.Append('\n');
        }

        return builder.ToString();
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

        var data = ExtractDataPack(page);

        return data
            .RootElement
            .EnumerateObject()
            .Where(x => x.Value.GetArrayLength() == 8 && x.Value[0].TryGetInt32(out int val) && val == 1)
            .Select(x =>
            {
                var item = x.Value[1];
                var props = item[item.GetArrayLength() - 1].GetProperty("2003");

                return new GoogleLensResult(
                    props[3].GetString()!,
                    props[2].GetString()!,
                    item[3][0].GetString()!,
                    props[12].GetString()!,
                    $"https://www.google.com/s2/favicons?sz=64&domain_url={props[17].GetString()!}");
            })
            .Where(x => x.ThumbnailUrl.StartsWith(Uri.UriSchemeHttp)) // Skip image URL with x-raw-image protocol
            .ToArray();
    }

    private static JsonDocument ExtractDataPack(byte[] page)
    {
        // Extract the JSON data pack from the page.
        var span = page.AsSpan();

        int start = span.IndexOf(ImageResultsStart);
        if (start == -1)
        {
            throw new GoogleLensException("Failed to extract the data pack.");
        }

        start += ImageResultsStart.Length;

        int end = span[start..].IndexOf(ImageResultsEnd);
        if (end == -1)
        {
            throw new GoogleLensException("Failed to extract the data pack.");
        }

        var rawObject = page.AsMemory(start, end);

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