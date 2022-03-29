using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a Bing reverse image search result.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)}}}")]
public class BingReverseImageSearchResult : IBingReverseImageSearchResult
{
    public BingReverseImageSearchResult(string url, string sourceUrl, string text, Color accentColor)
    {
        Url = url;
        SourceUrl = sourceUrl;
        Text = text;
        AccentColor = accentColor;
    }

    /// <inheritdoc/>
    public string Url { get; }

    /// <inheritdoc/>
    public string SourceUrl { get; }

    /// <inheritdoc/>
    public string Text { get; }

    /// <inheritdoc/>
    public Color AccentColor { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(Text)} = {Text}";

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => ToString();
}