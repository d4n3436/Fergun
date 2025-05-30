using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Apis.Google;

/// <summary>
/// Represents a Google Lens API wrapper.
/// </summary>
public interface IGoogleLensClient
{
    /// <summary>
    /// Performs OCR to the specified image URL.
    /// </summary>
    /// <param name="url">The URL of an image.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous OCR operation. The result contains the recognized text.</returns>
    Task<string> OcrAsync([StringSyntax(StringSyntaxAttribute.Uri)] string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs reverse image search to the specified image URL.
    /// </summary>
    /// <param name="url">The URL of an image.</param>
    /// <param name="language">The language to use when performing the search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous search operation. The result contains a read-only list of search results.</returns>
    Task<IReadOnlyList<IGoogleLensResult>> ReverseImageSearchAsync([StringSyntax(StringSyntaxAttribute.Uri)] string url, string? language = null, CancellationToken cancellationToken = default);
}