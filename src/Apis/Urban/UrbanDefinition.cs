using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Urban;

/// <summary>
/// Represents an Urban Dictionary definition.
/// </summary>
public class UrbanDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UrbanDefinition"/> class.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="date">The date this definition was posted on the front page as a word of the day.</param>
    /// <param name="permalink">A permalink to the page containing this definition.</param>
    /// <param name="thumbsUp">The number of thumps-up.</param>
    /// <param name="soundUrls">A collection of sound URLs.</param>
    /// <param name="author">The author of this definition.</param>
    /// <param name="word">The word (term) being defined.</param>
    /// <param name="id">The ID of this definition.</param>
    /// <param name="writtenOn">The date this definition was written.</param>
    /// <param name="example">An example usage of the definition.</param>
    /// <param name="thumbsDown">The number of thumps-down.</param>
    public UrbanDefinition(string definition, string? date, string permalink, int thumbsUp, IReadOnlyCollection<string>? soundUrls,
        string author, string word, int id, DateTimeOffset writtenOn, string example, int thumbsDown)
    {
        Definition = definition;
        Date = date;
        Permalink = permalink;
        ThumbsUp = thumbsUp;
        SoundUrls = soundUrls ?? [];
        Author = author;
        Word = word;
        Id = id;
        WrittenOn = writtenOn;
        Example = example;
        ThumbsDown = thumbsDown;
    }

    /// <summary>
    /// Gets the definition.
    /// </summary>
    [JsonPropertyName("definition")]
    public string Definition { get; }

    /// <summary>
    /// Gets the date this definition was posted on the front page as a word of the day.
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; }

    /// <summary>
    /// Gets a permalink to the page containing this definition.
    /// </summary>
    [JsonPropertyName("permalink")]
    public string Permalink { get; }

    /// <summary>
    /// Gets the number of thumps-up.
    /// </summary>
    [JsonPropertyName("thumbs_up")]
    public int ThumbsUp { get; }

    /// <summary>
    /// Gets a collection of sound URLs.
    /// </summary>
    [JsonPropertyName("sound_urls")]
    public IReadOnlyCollection<string> SoundUrls { get; }

    /// <summary>
    /// Gets the author of this definition.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; }

    /// <summary>
    /// Gets the word (term) being defined.
    /// </summary>
    [JsonPropertyName("word")]
    public string Word { get; }

    /// <summary>
    /// Gets the ID of this definition.
    /// </summary>
    [JsonPropertyName("defid")]
    public int Id { get; }

    /// <summary>
    /// Gets the date this definition was written.
    /// </summary>
    [JsonPropertyName("written_on")]
    public DateTimeOffset WrittenOn { get; }

    /// <summary>
    /// Gets an example usage of the definition.
    /// </summary>
    [JsonPropertyName("example")]
    public string Example { get; }

    /// <summary>
    /// Gets the number of thumps-down.
    /// </summary>
    [JsonPropertyName("thumbs_down")]
    public int ThumbsDown { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(Word)} = {Word}, {nameof(Definition)} = {Definition}";
}