namespace Fergun.Apis.Bing;

/// <summary>
/// Specifies the levels of safe search in Bing Visual Search.
/// </summary>
public enum BingSafeSearchLevel
{
    /// <summary>
    /// Return images with adult content. The thumbnail images are clear (non-fuzzy).
    /// </summary>
    Off = 0,

    /// <summary>
    /// Do not return images with adult content.
    /// </summary>
    Moderate = 1,

    /// <summary>
    /// Do not return images with adult content.
    /// </summary>
    Strict = 2
}