namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a Wikipedia article.
/// </summary>
public interface IWikipediaArticle : IPartialWikipediaArticle
{
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
}