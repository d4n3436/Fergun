namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a Yandex reverse image search result.
/// </summary>
public interface IYandexReverseImageSearchResult
{
    /// <summary>
    /// Gets a URL pointing to the image.
    /// </summary>
    string Url { get; }

    /// <summary>
    /// Gets a URL pointing to the webpage hosting the image.
    /// </summary>
    string SourceUrl { get; }

    /// <summary>
    /// Gets the title of the image result.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Gets the description of the image result.
    /// </summary>
    string Text { get; }
}