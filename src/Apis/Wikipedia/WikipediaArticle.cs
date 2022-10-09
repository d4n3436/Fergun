using System.Text.Json.Serialization;

namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a Wikipedia article.
/// </summary>
public class WikipediaArticle : IWikipediaArticle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WikipediaArticle"/> class.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="description">The description.</param>
    /// <param name="extract">The extract text.</param>
    /// <param name="image">The image.</param>
    /// <param name="id">The ID.</param>
    public WikipediaArticle(string title, string? description, string extract, WikipediaImage? image, int id)
    {
        Title = title;
        Description = description;
        Extract = extract;
        Image = image;
        Id = id;
    }

    /// <inheritdoc/>
    [JsonPropertyName("title")]
    public string Title { get; }

    /// <inheritdoc/>
    [JsonPropertyName("description")]
    public string? Description { get; }

    /// <inheritdoc/>
    [JsonPropertyName("extract")]
    public string Extract { get; }

    /// <inheritdoc cref="IWikipediaArticle.Image"/>
    [JsonPropertyName("original")]
    public WikipediaImage? Image { get; }

    /// <inheritdoc/>
    [JsonPropertyName("pageid")]
    public int Id { get; }

    /// <inheritdoc/>
    IWikipediaImage? IWikipediaArticle.Image => Image;

    /// <summary>
    /// Returns the title and description of this article.
    /// </summary>
    /// <returns>The title and description of this article.</returns>
    public override string ToString() => $"{Title} {(Description is null ? "" : $"({Description})")}";
}