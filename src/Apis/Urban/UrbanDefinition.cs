using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Urban;

/// <summary>
/// Represents an Urban Dictionary definition.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)}}}")]
public class UrbanDefinition
{
    [JsonConstructor]
    public UrbanDefinition(string definition, string? date, string permalink, int thumbsUp, IReadOnlyCollection<string>? soundUrls,
        string author, string word, int id, DateTimeOffset writtenOn, string example, int thumbsDown)
    {
        Definition = definition;
        Date = date;
        Permalink = permalink;
        ThumbsUp = thumbsUp;
        SoundUrls = soundUrls ?? Array.Empty<string>();
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
    public string Definition { get; init; }

    /// <summary>
    /// Gets the date this definition was posted on the front page as a word of the day.
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    /// <summary>
    /// Gets a permalink to the page containing this definition.
    /// </summary>
    [JsonPropertyName("permalink")]
    public string Permalink { get; init; }

    /// <summary>
    /// Gets the number of thumps-up.
    /// </summary>
    [JsonPropertyName("thumbs_up")]
    public int ThumbsUp { get; init; }

    /// <summary>
    /// Gets a collection of sound URLs.
    /// </summary>
    [JsonPropertyName("sound_urls")]
    public IReadOnlyCollection<string> SoundUrls { get; init; }

    /// <summary>
    /// Gets the author of this definition.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; init; }

    /// <summary>
    /// Gets the word (term) being defined.
    /// </summary>
    [JsonPropertyName("word")]
    public string Word { get; init; }

    /// <summary>
    /// Gets the ID of this definition.
    /// </summary>
    [JsonPropertyName("defid")]
    public int Id { get; init; }

    /// <summary>
    /// Gets the date this definition was written.
    /// </summary>
    [JsonPropertyName("written_on")]
    public DateTimeOffset WrittenOn { get; init; }

    /// <summary>
    /// Gets an example usage of the definition.
    /// </summary>
    [JsonPropertyName("example")]
    public string Example { get; init; }

    /// <summary>
    /// Gets the number of thumps-down.
    /// </summary>
    [JsonPropertyName("thumbs_down")]
    public int ThumbsDown { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(Word)} = {Word}, {nameof(Definition)} = {Definition}";

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => ToString();
}