namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Contains the minimal information used to identify a Wikipedia article.
/// </summary>
public interface IPartialWikipediaArticle
{
    /// <summary>
    /// Gets the title of this article.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the ID of this article.
    /// </summary>
    int Id { get; }
}