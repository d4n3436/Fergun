using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Dictionary;

/// <inheritdoc cref="IDictionaryEntryBlock"/>
public record DictionaryEntryBlock(
    [property: JsonPropertyName("definitions")] IReadOnlyList<DictionaryDefinition> Definitions,
    [property: JsonPropertyName("pos")] string PartOfSpeech,
    [property: JsonPropertyName("posSupplementaryInfo")] string SupplementaryInfo,
    [property: JsonPropertyName("pronunciation")] string Pronunciation) : IDictionaryEntryBlock
{
    /// <inheritdoc/>
    IReadOnlyList<IDictionaryDefinition> IDictionaryEntryBlock.Definitions => Definitions;

    /*
    [JsonPropertyName("definitionSets")]
    public IReadOnlyList<DefinitionSet> DefinitionSets { get; } // used in scientific definitions (a)
    */
}

/*
public class DefinitionSet
{
    [JsonPropertyName("predefinitionContent")]
    public string PredefinitionContent { get; }

    [JsonPropertyName("definition")]
    public string Definition { get; }

    [JsonPropertyName("postdefinitionContent")]
    public string PostdefinitionContent { get; }

    [JsonPropertyName("subdefinitions")]
    public IReadOnlyList<DefinitionSet> Subdefinitions { get; }

    [JsonPropertyName("dsSupplementaryInfo")]
    public IReadOnlyList<object> DsSupplementaryInfo { get; }
}
*/