using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Fergun.Apis.Yandex;

/// <summary>
/// Represents a Yandex reverse image search result.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)}}}")]
public class YandexReverseImageSearchResult : IYandexReverseImageSearchResult
{
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

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => ToString();
}