using Fergun.Modules;

namespace Fergun;

/// <summary>
/// Specifies the reverse image search engines used in <see cref="ImageModule"/>.
/// </summary>
public enum ReverseImageSearchEngine
{
    /// <summary>
    /// Bing.
    /// </summary>
    Bing = 0,

    /// <summary>
    /// Yandex.
    /// </summary>
    Yandex = 1,

    /// <summary>
    /// Google.
    /// </summary>
    Google = 2
}