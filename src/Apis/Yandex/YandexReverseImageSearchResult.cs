using System.Text.Json.Serialization;

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
    /// <param name="snippet">Snippet data.</param>
    public YandexReverseImageSearchResult(string url, YandexSnippetData snippet)
    {
        Url = url;
        Snippet = snippet;
    }

    /// <inheritdoc/>
    [JsonPropertyName("img_href")]
    public string Url { get; }

    [JsonPropertyName("snippet")]
    public YandexSnippetData Snippet { get; }

    /// <inheritdoc/>
    public string SourceUrl => Snippet.SourceUrl;

    /// <inheritdoc/>
    public string? Title => Snippet.Title;

    /// <inheritdoc/>
    public string Text => Snippet.Text;

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(Title)} = {Title ?? "(None)"}, {nameof(Text)} = {Text}";

    public class YandexSnippetData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="YandexSnippetData"/> class.
        /// </summary>
        /// <param name="sourceUrl">A URL pointing to the webpage hosting the image.</param>
        /// <param name="title">The title of the image result.</param>
        /// <param name="text">The description of the image result.</param>
        public YandexSnippetData(string sourceUrl, string? title, string text)
        {
            SourceUrl = sourceUrl;
            Title = title;
            Text = text;
        }

        [JsonPropertyName("url")]
        public string SourceUrl { get; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(HtmlEncodingConverter))]
        public string? Title { get; }

        [JsonPropertyName("text")]
        [JsonConverter(typeof(HtmlEncodingConverter))]
        public string Text { get; }
    }
}