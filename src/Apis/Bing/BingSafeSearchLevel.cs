namespace Fergun.Apis.Bing;

/// <summary>
/// Specifies the levels of safe search in Bing Visual Search.
/// </summary>
public enum BingSafeSearchLevel
{
    /// <summary>
    /// Return images with adult content. The thumbnail images are clear (non-fuzzy).
    /// </summary>
    Off,
    /// <summary>
    /// Do not return images with adult content.
    /// </summary>
    Moderate,
    /// <summary>
    /// Do not return images with adult content.
    /// </summary>
    Strict
}