namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a Bing Visual Search API.
/// </summary>
public interface IBingVisualSearch
{
    /// <summary>
    /// Performs OCR to the specified image URL.
    /// </summary>
    /// <param name="url">The URL of an image.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous OCR operation. The result contains the recognized text.</returns>
    Task<string?> OcrAsync(string url);
}