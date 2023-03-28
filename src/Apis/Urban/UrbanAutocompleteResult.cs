using System.Text.Json.Serialization;

namespace Fergun.Apis.Urban;

/// <summary>
/// Represent an Urban Dictionary autocomplete result.
/// </summary>
public class UrbanAutocompleteResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UrbanAutocompleteResult"/> class.
    /// </summary>
    /// <param name="term">The term of this result.</param>
    /// <param name="preview">A preview definition of the term.</param>
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
    public override string ToString() => $"{nameof(Term)} = {Term}, {nameof(Preview)} = {Preview}";
}