using System.Collections.Generic;

namespace Fergun.Apis.WolframAlpha;

/// <summary>
/// Represents a WolframAlpha pod.
/// </summary>
public interface IWolframAlphaPod
{
    /// <summary>
    /// Gets the ID of this pod.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the title of this pod.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the position of this pod.
    /// </summary>
    int Position { get; }

    /// <summary>
    /// Gets the sub-pods.
    /// </summary>
    IReadOnlyList<IWolframAlphaSubPod> SubPods { get; }
}