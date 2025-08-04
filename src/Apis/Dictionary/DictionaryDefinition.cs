using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryDefinition"/>
[UsedImplicitly]
public class DictionaryDefinition : IDictionaryDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryDefinition"/> class.
    /// </summary>
    /// <param name="predefinitionContent">The content that is displayed before <see cref="Definition"/>.</param>
    /// <param name="postdefinitionContent">The content that is displayed after <see cref="Definition"/>.</param>
    /// <param name="definition">The definition itself.</param>
    /// <param name="subdefinitions">The sub-definitions.</param>
    public DictionaryDefinition(string predefinitionContent, string postdefinitionContent,
        string? definition, IReadOnlyList<DictionaryDefinition>? subdefinitions)
    {
        PredefinitionContent = predefinitionContent;
        PostdefinitionContent = postdefinitionContent;
        Definition = definition;
        Subdefinitions = subdefinitions ?? [];
    }

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