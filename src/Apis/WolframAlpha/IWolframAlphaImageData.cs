using System.Diagnostics.CodeAnalysis;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents an image within a <see cref="IWolframAlphaSubPod"/>.
/// </summary>
public interface IWolframAlphaImageData
{
    /// <summary>
    /// Gets a descriptive title used for internal identification of an image.
    /// </summary>
    string AltText { get; }

    /// <summary>
    /// Gets the binary data of the image.
    /// </summary>
    byte[]? Data { get; }

    /// <summary>
    /// Gets the URL of the image.
    /// </summary>
    string? SourceUrl { get; }

    /// <summary>
    /// Gets a value indicating whether the binary data of the image (<see cref="Data"/>) is present instead of <see cref="SourceUrl"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Data))]
    [MemberNotNullWhen(false, nameof(SourceUrl))]
    bool IsDataPresent { get; }

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