using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Urban;

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
    public override string ToString() => $"{nameof(Term)} = {Term}, {nameof(Preview)} = {Preview}";

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => ToString();
}