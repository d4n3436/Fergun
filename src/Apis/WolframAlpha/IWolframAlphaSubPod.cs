namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a WolframAlpha sub-pod.
/// </summary>
public interface IWolframAlphaSubPod
{
    /// <summary>
    /// Gets the image contained in this sub-pod.
    /// </summary>
    IWolframAlphaImageData Image { get; }

    /// <summary>
    /// Gets the text representation of this sub-pod.
    /// </summary>
    string PlainText { get; }

    /// <summary>
    /// Gets the title of this sub-pod.
    /// </summary>
    string Title { get; }
}