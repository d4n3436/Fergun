using System.Drawing;

namespace Fergun.Apis.Bing;

/// <summary>
/// Represents a Bing reverse image search result.
/// </summary>
public interface IBingReverseImageSearchResult
{
    /// <summary>
    /// Gets a URL pointing to the image.
    /// </summary>
    string Url { get; }

    /// <summary>
    /// Gets the friendly domain name.
    /// </summary>
    string? FriendlyDomainName { get; }

    /// <summary>
    /// Gets a URL pointing to the webpage hosting the image.
    /// </summary>
    string SourceUrl { get; }

    /// <summary>
    /// Gets the description of the image result.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Gets the accent color of this result.
    /// </summary>
    Color AccentColor { get; }
}