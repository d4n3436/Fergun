using System.Text.Json.Serialization;

namespace Fergun.Apis.Wikipedia;

/// <summary>
/// Represents a Wikipedia image.
/// </summary>
public class WikipediaImage : IWikipediaImage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WikipediaImage"/> class.
    /// </summary>
    /// <param name="url">The URL of the image.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public WikipediaImage(string url, int width, int height)
    {
        Url = url;
        Width = width;
        Height = height;
    }

    /// <inheritdoc/>
    [JsonPropertyName("source")]
    public string Url { get; }

    /// <inheritdoc/>
    [JsonPropertyName("width")]
    public int Width { get; }

    /// <inheritdoc/>
    [JsonPropertyName("height")]
    public int Height { get; }

    /// <summary>
    /// Returns <see cref="Url"/>.
    /// </summary>
    /// <returns><see cref="Url"/>.</returns>
    public override string ToString() => Url;
}