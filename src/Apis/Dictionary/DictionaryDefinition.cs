using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryDefinition"/>
public class DictionaryDefinition : IDictionaryDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryDefinition"/> class.
    /// </summary>
    ///  <param name="ordinal">The order number of this definition.</param>
    /// <param name="order">The order number of this definition.</param>
    /// <param name="predefinitionContent">The content that is displayed before <see cref="Definition"/>.</param>
    /// <param name="postdefinitionContent">The content that is displayed after <see cref="Definition"/>.</param>
    /// <param name="definition">The definition itself.</param>
    /// <param name="subdefinitions">The sub-definitions.</param>
    public DictionaryDefinition(int? ordinal, int? order, string predefinitionContent, string postdefinitionContent,
        string? definition, IReadOnlyList<DictionaryDefinition>? subdefinitions)
    {
        Ordinal = ordinal;
        Order = order;
        PredefinitionContent = predefinitionContent;
        PostdefinitionContent = postdefinitionContent;
        Definition = definition;
        Subdefinitions = subdefinitions ?? [];
    }

    /// <inheritdoc/>
    [JsonPropertyName("ordinal")]
    public int? Ordinal { get; }

    /// <inheritdoc/>
    [JsonPropertyName("order")]
    public int? Order { get; }

    /// <inheritdoc/>
    [JsonPropertyName("predefinitionContent")]
    public string PredefinitionContent { get; }

    /// <inheritdoc/>
    [JsonPropertyName("postdefinitionContent")]
    public string PostdefinitionContent { get; }

    /// <inheritdoc/>
    [JsonPropertyName("definition")]
    public string? Definition { get; }

    /// <inheritdoc cref="IDictionaryDefinition.Subdefinitions"/>
    [JsonPropertyName("subdefinitions")]
    public IReadOnlyList<DictionaryDefinition> Subdefinitions { get; }

    /// <inheritdoc/>
    IReadOnlyList<IDictionaryDefinition> IDictionaryDefinition.Subdefinitions => Subdefinitions;
}