using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fergun.Apis;

/// <summary>
/// Represents an API wrapper for Urban Dictionary.
/// </summary>
public sealed class UrbanDictionary : IDisposable
{
    private static readonly Uri _apiEndpoint = new("https://api.urbandictionary.com/v0/");

    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36";
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UrbanDictionary"/> class.
    /// </summary>
    public UrbanDictionary()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UrbanDictionary"/> class using the specified <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">An instance of <see cref="HttpClient"/>.</param>
    public UrbanDictionary(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress ??= _apiEndpoint;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
        }
    }

    /// <summary>
    /// Gets definitions for a term.
    /// </summary>
    /// <param name="term">The term to search.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of definitions.</returns>
    public async Task<IEnumerable<UrbanDefinition>> GetDefinitionsAsync(string term)
    {
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"define?term={Uri.EscapeDataString(term)}", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IEnumerable<UrbanDefinition>>()!;
    }

    /// <summary>
    /// Gets random definitions.
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of random definitions.</returns>
    public async Task<IEnumerable<UrbanDefinition>> GetRandomDefinitionsAsync()
    {
        await using var stream = await _httpClient.GetStreamAsync(new Uri("random", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IEnumerable<UrbanDefinition>>()!;
    }

    /// <summary>
    /// Gets a definition by its ID.
    /// </summary>
    /// <param name="id">The ID of the definition.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the definition, or <c>null</c> if not found.</returns>
    public async Task<UrbanDefinition?> GetDefinitionAsync(int id)
    {
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"define?defid={id}", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var list = document.RootElement.GetProperty("list");

        return list.GetArrayLength() == 0 ? null : list[0].Deserialize<UrbanDefinition>()!;
    }

    /// <summary>
    /// Gets the words of the day.
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of definitions.</returns>
    public async Task<IEnumerable<UrbanDefinition>> GetWordsOfTheDayAsync()
    {
        await using var stream = await _httpClient.GetStreamAsync(new Uri("words_of_the_day", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("list").Deserialize<IEnumerable<UrbanDefinition>>()!;
    }

    /// <summary>
    /// Gets autocomplete results for a term.
    /// </summary>
    /// <param name="term">The term to search.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of suggested terms.</returns>
    public async Task<IEnumerable<string>> GetAutocompleteResultsAsync(string term)
    {
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"autocomplete?term={Uri.EscapeDataString(term)}", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.Deserialize<IEnumerable<string>>()!;
    }

    /// <summary>
    /// Gets autocomplete results for a term. The results contain the term and a preview definition.
    /// </summary>
    /// <param name="term">The term to search.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a read-only collection of suggested terms.</returns>
    public async Task<IEnumerable<UrbanAutocompleteResult>> GetAutocompleteResultsExtraAsync(string term)
    {
        await using var stream = await _httpClient.GetStreamAsync(new Uri($"autocomplete-extra?term={Uri.EscapeDataString(term)}", UriKind.Relative)).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return document.RootElement.GetProperty("results").Deserialize<IEnumerable<UrbanAutocompleteResult>>()!;
    }

    /// <inheritdoc/>
    public void Dispose() => Dispose(true);

    /// <inheritdoc cref="Dispose()"/>
    private void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Represent an Urban Dictionary autocomplete result.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)}}}")]
public class UrbanAutocompleteResult
{
    [JsonConstructor]
    public UrbanAutocompleteResult(string term, string preview)
    {
        Term = term;
        Preview = preview;
    }

    /// <summary>
    /// Gets the term of this result.
    /// </summary>
    [JsonPropertyName("term")]
    public string Term { get; }

    /// <summary>
    /// Gets a preview definition of the term.
    /// </summary>
    [JsonPropertyName("preview")]
    public string Preview { get; }

    /// <inheritdoc/>
    public override string ToString() => $"Term = {Term}, Preview = {Preview}";

    private string DebuggerDisplay => ToString();
}

/// <summary>
/// Represents an Urban Dictionary definition.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)}}}")]
public class UrbanDefinition
{
    [JsonConstructor]
    public UrbanDefinition(string definition, string? date, string permalink, int thumbsUp, IReadOnlyCollection<string> soundUrls,
        string author, string word, int id, DateTimeOffset writtenOn, string example, int thumbsDown)
    {
        Definition = definition;
        Date = date;
        Permalink = permalink;
        ThumbsUp = thumbsUp;
        SoundUrls = soundUrls;
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
    public override string ToString() => $"Word = {Word}, Definition = {Definition}";

    private string DebuggerDisplay => ToString();
}