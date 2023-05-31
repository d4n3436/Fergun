namespace Fergun.Apis.Google;

/// <summary>
/// Represents a Google Lens image result.
/// </summary>
public interface IGoogleLensResult
{
    /// <summary>
    /// Gets the title of the result.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the URL of the page the search result comes from.
    /// </summary>
    string SourcePageUrl { get; }

    /// <summary>
    /// Gets the thumbnail URL.
    /// </summary>
    string ThumbnailUrl { get; }

    /// <summary>
    /// Gets the name of the domain the search result comes from.
    /// </summary>
    string SourceDomainName { get; }

    /// <summary>
    /// Gets the URL of the source icon.
    /// </summary>
    string SourceIconUrl { get; }
}