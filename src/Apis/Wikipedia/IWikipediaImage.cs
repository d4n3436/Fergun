namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a Wikipedia image.
/// </summary>
public interface IWikipediaImage
{
    /// <summary>
    /// Gets the URL of the image.
    /// </summary>
    string Url { get; }

    /// <summary>
    /// Gets the width of this image.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of this image.
    /// </summary>
    int Height { get; }
}