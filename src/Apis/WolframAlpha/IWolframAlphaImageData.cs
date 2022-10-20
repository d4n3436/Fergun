namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents an image within a <see cref="IWolframAlphaSubPod"/>.
/// </summary>
public interface IWolframAlphaImageData
{
    /// <summary>
    /// Gets the URL of the image.
    /// </summary>
    string SourceUrl { get; }

    /// <summary>
    /// Gets the width of the image.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of the image.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets the content-type of the image.
    /// </summary>
    string ContentType { get; }
}