using Discord.Interactions;
using Fergun.Modules;

namespace Fergun.Common;

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
    [Hide] // TODO: Remove when Google Lens reverse image search is fixed
    Google = 2
}