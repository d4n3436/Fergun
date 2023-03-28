namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a Yandex reverse image search result.
/// </summary>
public class YandexReverseImageSearchResult : IYandexReverseImageSearchResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YandexReverseImageSearchResult"/> class.
    /// </summary>
    /// <param name="url">A URL pointing to the image.</param>
    /// <param name="sourceUrl">A URL pointing to the webpage hosting the image.</param>
    /// <param name="title">The title of the image result.</param>
    /// <param name="text">The description of the image result.</param>
    internal YandexReverseImageSearchResult(string url, string sourceUrl, string? title, string text)
    {
        Url = url;
        SourceUrl = sourceUrl;
        Title = title;
        Text = text;
    }

    /// <inheritdoc/>
    public string Url { get; }

    /// <inheritdoc/>
    public string SourceUrl { get; }

    /// <inheritdoc/>
    public string? Title { get; }

    /// <inheritdoc/>
    public string Text { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(Title)} = {Title ?? "(None)"}, {nameof(Text)} = {Text}";
}