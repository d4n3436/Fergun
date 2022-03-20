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
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous OCR operation. The result contains the recognized text.</returns>
    Task<string?> OcrAsync(string url);
}