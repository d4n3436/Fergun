using Fergun.Modules;

namespace Fergun;

/// <summary>
/// Specifies the search types in Urban Dictionary. Used on <see cref="UrbanModule"/>.
/// </summary>
public enum UrbanSearchType
{
    /// <summary>
    /// Regular search.
    /// </summary>
    Search = 0,

    /// <summary>
    /// Random words search.
    /// </summary>
    Random = 1,

    /// <summary>
    /// Get the words of the day.
    /// </summary>
    WordsOfTheDay = 2
}