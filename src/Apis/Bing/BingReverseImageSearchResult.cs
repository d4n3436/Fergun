using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text.Json.Serialization;

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
    [JsonPropertyName("contentUrl")]
    public string Url { get; }

    /// <inheritdoc/>
    [JsonPropertyName("hostPageUrl")]
    public string SourceUrl { get; }

    /// <inheritdoc/>
    [JsonPropertyName("name")]
    public string Text { get; }

    /// <inheritdoc/>
    [JsonPropertyName("accentColor")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color AccentColor { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(Text)} = {Text}";

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => ToString();
}