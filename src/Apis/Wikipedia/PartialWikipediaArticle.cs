using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a class containing the minimal information used to identify a Wikipedia article.
/// </summary>
[UsedImplicitly]
public class PartialWikipediaArticle : IPartialWikipediaArticle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartialWikipediaArticle"/> class.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="id">The ID.</param>
    public PartialWikipediaArticle(string title, int id)
    {
        Title = title;
        Id = id;
    }

    /// <inheritdoc/>
    [JsonPropertyName("title")]
    public string Title { get; }

    /// <inheritdoc/>
    [JsonPropertyName("pageid")]
    public int Id { get; }
}