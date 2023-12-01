using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a Yandex Image Search API.
/// </summary>
public interface IYandexImageSearch
{
    /// <summary>
    /// Performs OCR to the specified image URL.
    /// </summary>
    /// <param name="url">The URL of an image.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous OCR operation. The result contains the recognized text.</returns>
    Task<string?> OcrAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs reverse image search to the specified image URL.
    /// </summary>
    /// <param name="url">The URL of an image.</param>
    /// <param name="mode">The search filter mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous search operation. The result contains a read-only list of search results.</returns>
    Task<IReadOnlyList<IYandexReverseImageSearchResult>> ReverseImageSearchAsync(string url,
        YandexSearchFilterMode mode = YandexSearchFilterMode.Moderate, CancellationToken cancellationToken = default);
}