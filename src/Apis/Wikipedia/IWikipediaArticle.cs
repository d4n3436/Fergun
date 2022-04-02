namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represent a Wikipedia article.
/// </summary>
public interface IWikipediaArticle
{
    /// <summary>
    /// Gets the title of this article.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the description of this article.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets the extract text of this article.
    /// </summary>
    string Extract { get; }

    /// <summary>
    /// Gets the image of this article.
    /// </summary>
    IWikipediaImage? Image { get; }

    /// <summary>
    /// Gets the ID of this article.
    /// </summary>
    int Id { get; }
}