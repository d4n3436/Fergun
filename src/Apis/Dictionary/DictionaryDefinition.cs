using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryDefinition"/>
public class DictionaryDefinition : IDictionaryDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryDefinition"/> class.
    /// </summary>
    /// <param name="order">The order of this definition.</param>
    /// <param name="predefinitionContent">The content that is displayed before <see cref="Definition"/>.</param>
    /// <param name="postdefinitionContent">The content that is displayed after <see cref="Definition"/>.</param>
    /// <param name="definition">The definition itself.</param>
    /// <param name="subdefinitions">The sub-definitions.</param>
    public DictionaryDefinition(int order, string predefinitionContent, string postdefinitionContent,
        string definition, IReadOnlyList<DictionaryDefinition>? subdefinitions)
    {
        Order = order;
        PredefinitionContent = predefinitionContent;
        PostdefinitionContent = postdefinitionContent;
        Definition = definition;
        Subdefinitions = subdefinitions ?? Array.Empty<DictionaryDefinition>();
    }

    /// <inheritdoc/>
    [JsonPropertyName("order")]
    public int Order { get; }

    /// <inheritdoc/>
    [JsonPropertyName("predefinitionContent")]
    public string PredefinitionContent { get; }

    /// <inheritdoc/>
    [JsonPropertyName("postdefinitionContent")]
    public string PostdefinitionContent { get; }

    /// <inheritdoc/>
    [JsonPropertyName("definition")]
    public string Definition { get; }

    /// <inheritdoc cref="IDictionaryDefinition.Subdefinitions"/>
    [JsonPropertyName("subdefinitions")]
    public IReadOnlyList<DictionaryDefinition> Subdefinitions { get; }

    /// <inheritdoc/>
    IReadOnlyList<IDictionaryDefinition> IDictionaryDefinition.Subdefinitions => Subdefinitions;

    // Always empty
    /*
    [JsonPropertyName("synonyms")]
    public string Synonyms { get; init; }

    [JsonPropertyName("antonyms")]
    public string Antonyms { get; init; }
    */
}