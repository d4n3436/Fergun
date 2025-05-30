using JetBrains.Annotations;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a Bing reverse image search result.
/// </summary>
[UsedImplicitly]
public class BingReverseImageSearchResult : IBingReverseImageSearchResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BingReverseImageSearchResult"/> class.
    /// </summary>
    /// <param name="url">A URL pointing to the image.</param>
    /// <param name="friendlyDomainName">The friendly domain name.</param>
    /// <param name="sourceUrl">A URL pointing to the webpage hosting the image.</param>
    /// <param name="text">The description of the image result.</param>
    /// <param name="accentColor">The accent color of this result.</param>
    public BingReverseImageSearchResult(string url, string? friendlyDomainName, string sourceUrl, string text, Color accentColor)
    {
        Url = url;
        FriendlyDomainName = friendlyDomainName;
        SourceUrl = sourceUrl;
        Text = text;
        AccentColor = accentColor;
    }

    /// <inheritdoc/>
    [JsonPropertyName("contentUrl")]
    public string Url { get; }

    /// <inheritdoc/>
    [JsonPropertyName("hostPageDomainFriendlyName")]
    public string? FriendlyDomainName { get; }

    /// <inheritdoc/>
    [JsonPropertyName("hostPageUrl")]
    public string SourceUrl { get; }

    /// <inheritdoc/>
    [JsonPropertyName("name")]
    public string Text { get; }

    /// <inheritdoc/>
    [JsonPropertyName("accentColor")]
    [JsonConverter(typeof(ColorConverter))]
    public Color AccentColor { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(Text)} = {Text}";
}